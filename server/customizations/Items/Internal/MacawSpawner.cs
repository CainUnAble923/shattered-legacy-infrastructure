// ServUO: Items/Internal/MacawSpawner.cs (cc-P129): MacawSpawner (the gold foil sheet that is really a trap) and
// MacawNest, one file as in ServUO. Values verbatim; serialization by the generator: the nest and the spawned list are
// [SerializableField]s, and ServUO's Deserialize rewiring (each macaw's MacawSpawner, the nest's Foil) is
// [AfterDeserialization]. ServUO's Delete() overrides are pinned's OnAfterDelete.
//
// Generate() and its 23 Ter Mur points are ported as written. ServUO calls it from its Time of Legends generator
// (Services/Expansions/TimeOfLegends.cs:81); nothing calls it here, so no nest exists until staff place one
// ([add MacawNest) or something calls MacawSpawner.Generate(). That is for Chase (the notes).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MacawSpawner : Item
{
    public override int LabelNumber => 1124032; // foil sheet

    [SerializableField(0, setter: "private")]
    private List<Mobile> _spawn;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private MacawNest _addon;

    public bool Moving { get; private set; }

    [Constructible]
    public MacawSpawner() : base(0x9C48)
    {
        _spawn = new List<Mobile>();
        Movable = false;
        Hue = 1281;

        _addon = new MacawNest();
        _addon.Foil = this;
    }

    public override bool VerifyMove(Mobile from)
    {
        if (Visible && Map != null && Map != Map.Internal && Utility.RandomBool())
        {
            SpawnBirdies(from);
        }

        return base.VerifyMove(from);
    }

    public void SpawnBirdies(Mobile from)
    {
        var count = Utility.RandomMinMax(3, 5);

        for (var i = 0; i < count; i++)
        {
            var p = Location;

            for (var j = 0; j < 10; j++)
            {
                var x = Utility.RandomMinMax(X - 1, X + 1);
                var y = Utility.RandomMinMax(Y - 1, Y + 2);
                var z = Map.GetAverageZ(x, y);

                if (Map.CanSpawnMobile(x, y, z))
                {
                    p = new Point3D(x, y, z);
                    break;
                }
            }

            BaseCreature macaw = new Macaw(this);
            macaw.MoveToWorld(p, Map);
            Spawn.Add(macaw);

            Visible = false;
            Timer.DelayCall(TimeSpan.FromSeconds(Utility.RandomMinMax(60, 90)), () => Visible = true);
        }

        Spawn.ForEach(s => s.Combatant = from);
    }

    public override void OnLocationChange(Point3D oldLocation)
    {
        if (Location != oldLocation && Addon != null && !Moving)
        {
            MoveToNest();
        }

        base.OnLocationChange(oldLocation);
    }

    public override void OnMapChange()
    {
        if (Addon != null)
        {
            Addon.Map = Map;
        }

        base.OnMapChange();
    }

    private void MoveToNest()
    {
        Addon.MoveToWorld(Location, Map);
        Moving = true;
        MoveToWorld(new Point3D(Addon.X + 1, Addon.Y, Addon.Z + 9), Addon.Map);
        Moving = false;
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (Addon != null)
        {
            Addon.Foil = null;
            Addon.Delete();
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _spawn ??= new List<Mobile>();

        for (var i = _spawn.Count - 1; i >= 0; i--)
        {
            if (_spawn[i] is Macaw macaw)
            {
                macaw.MacawSpawner = this;
            }
            else
            {
                _spawn.RemoveAt(i);
            }
        }

        if (_addon != null)
        {
            _addon.Foil = this;
        }
    }

    public static void Generate()
    {
        foreach (var pnt in _spawnLocs)
        {
            var nest = new MacawNest();
            nest.MoveToWorld(pnt, Map.TerMur);
        }
    }

    private static readonly Point3D[] _spawnLocs =
    {
        new(491, 1863, 95),
        new(496, 1865, 85),
        new(496, 1852, 85),
        new(490, 1867, 75),
        new(494, 1859, 95),
        new(498, 1845, 75),
        new(502, 1859, 55),
        new(484, 1871, 55),
        new(476, 1867, 75),
        new(500, 1864, 65),
        new(484, 1865, 85),
        new(500, 1852, 65),
        new(493, 1847, 94),
        new(479, 1869, 65),
        new(502, 1871, 55),
        new(502, 1848, 55),
        new(498, 1860, 75),
        new(498, 1849, 75),
        new(500, 1845, 65),
        new(472, 1871, 55),
        new(495, 1869, 65),
        new(490, 1871, 55),
        new(478, 1865, 85)
    };
}

[SerializationGenerator(0, false)]
public partial class MacawNest : BaseAddon
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private MacawSpawner _foil;

    [Constructible]
    public MacawNest()
    {
        AddComponent(new AddonComponent(3235), 0, 0, 0);
        AddComponent(new AddonComponent(3232), 0, 0, 0);
        AddComponent(new AddonComponent(3236), 1, 0, 0);
        AddComponent(new AddonComponent(3232), 1, -1, 0);
        AddComponent(new AddonComponent(3231), 0, -1, 0);
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (Foil != null)
        {
            Foil.Addon = null;
            Foil.Delete();
        }
    }
}
