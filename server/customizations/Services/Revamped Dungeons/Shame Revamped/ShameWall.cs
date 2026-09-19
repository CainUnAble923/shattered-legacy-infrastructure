// ServUO: Services/Revamped Dungeons/Shame Revamped/ShameWall.cs (CC4 Shame).
//
// The three cave-troll walls on level 1 (two facets, six walls): an addon of three or four wall pieces with a
// CaveTroll "the wall guardian" spawned beside it. Killing the troll drops the whole addon 50 z and clears its
// Visible flag; two minutes later Reset() puts it back at StartSpot, sets Visible, and spawns a fresh troll.
// Using a component while the troll is dead also resets it. AddTeleporters places a ShameWallTeleporter on the
// tile in front of and behind each piece, replacing any stock ConditionTeleporter there; those teleporters
// work only while the addon's Visible flag is set (ShameTeleporter.cs).
//
// Visible is the wall's "standing" flag: OnTrollKilled clears it as the wall sinks, Reset sets it as the wall
// comes back, and the wall teleporters read it. BaseAddon's constructor sets it false on both emulators and
// ServUO's ShameWall constructor never sets it true, so as shipped a fresh wall's teleporters are inert until
// that wall's troll has died once (Q-046). The constructor here sets it, which is the one deviation in this
// file (notes/cc4-shame.md §9.1, D-10/D-16 precedent: ServUO's literal value is the bug).
//
// Conversion: the four serialized members are generator fields in ServUO's order. ServUO's version-1 and
// version-2 upgrade paths (re-adding teleporters to old saves) are dropped. OnAfterDelete deletes a live
// troll, which ServUO leaves behind when [DeleteShame removes the wall (GM tooling, paper-only).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.ShameRevamped;

[SerializationGenerator(0, false)]
public partial class ShameWall : BaseAddon
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private CaveTroll _troll;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _startSpot;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Map _startMap;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _trollSpawnLoc;

    public ShameWall(Dictionary<Point3D, int> details, Point3D startSpot, Point3D trollSpawnLoc, Map map)
    {
        foreach (var (offset, itemId) in details)
        {
            AddComponent(new AddonComponent(itemId), offset.X, offset.Y, offset.Z);
        }

        _startSpot = startSpot;
        _startMap = map;
        _trollSpawnLoc = trollSpawnLoc;

        SpawnTroll();

        // Q-046: ServUO never sets this in the constructor, so a freshly generated wall is standing with a live
        // troll but flagged as sunk, and its teleporters are inert until that wall's troll has died once. Reset()
        // sets it; this makes a fresh wall match a reset one. Deviation, same shape as D-10/D-16.
        Visible = true;
    }

    public void OnTrollKilled()
    {
        Z -= 50;
        Visible = false;

        Timer.StartTimer(TimeSpan.FromMinutes(2), Reset);

        Troll = null;
    }

    public void Reset()
    {
        if (Deleted)
        {
            return;
        }

        MoveToWorld(StartSpot, StartMap);
        Visible = true;
        SpawnTroll();
    }

    private void SpawnTroll()
    {
        if (Troll == null || Troll.Deleted || !Troll.Alive)
        {
            Troll = new CaveTroll(this);
            Troll.MoveToWorld(TrollSpawnLoc, StartMap);

            Troll.Home = TrollSpawnLoc;
            Troll.RangeHome = 8;
        }
    }

    public override void OnComponentUsed(AddonComponent component, Mobile from)
    {
        base.OnComponentUsed(component, from);

        if (Troll == null || Troll.Deleted || !Troll.Alive)
        {
            Reset();
        }
    }

    public static void AddTeleporters(ShameWall wall)
    {
        var map = wall.Map;

        if (map == null || map == Map.Internal)
        {
            return;
        }

        foreach (var component in wall.Components)
        {
            foreach (var pnts in _teleportLocs)
            {
                if (component.Location != pnts[0])
                {
                    continue;
                }

                ConditionTeleporter oldTele = null;

                foreach (var item in map.GetItemsInRange(pnts[1], 0))
                {
                    if (item is ConditionTeleporter ct && item.Location == pnts[1])
                    {
                        oldTele = ct;
                        break;
                    }
                }

                if (oldTele != null)
                {
                    WeakEntityCollection.Remove(ShameGenerator.CollectionKey, oldTele);
                    oldTele.Delete();
                }

                var teleporter = new ShameWallTeleporter(pnts[2], map);
                teleporter.MoveToWorld(pnts[1], map);

                WeakEntityCollection.Add(ShameGenerator.CollectionKey, teleporter);
            }
        }
    }

    // { wall piece, tile the teleporter stands on, tile it sends you to }
    private static readonly Point3D[][] _teleportLocs =
    {
        new[] { new Point3D(5402, 82, 10), new Point3D(5402, 81, 10), new Point3D(5402, 83, 10) },
        new[] { new Point3D(5403, 82, 10), new Point3D(5403, 81, 10), new Point3D(5403, 83, 10) },
        new[] { new Point3D(5404, 82, 10), new Point3D(5404, 81, 10), new Point3D(5404, 83, 10) },
        new[] { new Point3D(5405, 82, 10), new Point3D(5405, 81, 10), new Point3D(5405, 83, 10) },

        new[] { new Point3D(5465, 25, -10), new Point3D(5464, 25, -10), new Point3D(5466, 25, -10) },
        new[] { new Point3D(5465, 26, -10), new Point3D(5464, 26, -10), new Point3D(5466, 26, -10) },
        new[] { new Point3D(5465, 27, -10), new Point3D(5464, 27, -10), new Point3D(5466, 27, -10) },
        new[] { new Point3D(5465, 28, -10), new Point3D(5464, 28, -10), new Point3D(5466, 28, -10) },

        new[] { new Point3D(5618, 57, 0), new Point3D(5618, 58, 0), new Point3D(5618, 56, 0) },
        new[] { new Point3D(5619, 57, 0), new Point3D(5619, 58, 0), new Point3D(5619, 56, 0) },
        new[] { new Point3D(5620, 57, 0), new Point3D(5620, 58, 0), new Point3D(5620, 56, 0) }
    };

    public static int TeleporterPairCount => _teleportLocs.Length;

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (Troll != null)
        {
            Troll.Wall = this;
        }

        if (Location != StartSpot || Troll == null || Troll.Deleted || !Troll.Alive)
        {
            Reset();
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (_troll?.Deleted == false)
        {
            _troll.Delete();
        }
    }
}
