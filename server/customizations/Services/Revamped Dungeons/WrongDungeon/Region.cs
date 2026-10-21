// ServUO: Services/Revamped Dungeons/WrongDungeon/Region.cs (cc-P194).
//
// Two regions inside the Wrong dungeon. ServUO declares them in Data/Regions.xml as children of the "Wrong"
// DungeonRegion on both facets:
//
//   <region type="WrongLevel3">            <rect x="5687" y="623" width="33" height="44" />
//     <region type="WrongJail">            <rect x="5700" y="636" width="7" height="7" />
//
// Pinned ModernUO reads its regions from Data/regions.json, a data file apply-patches.sh does not carry, so the
// two are registered here at Initialize (Main.cs invokes Initialize after RegionJsonSerializer.LoadRegions, so
// the static "Wrong" region exists and is used as the parent; a player in the jail is still IsPartOf("Wrong"),
// which the hot-item curse relies on). Priority is DefaultPriority + 1, the Despise rule: pinned constructs
// every region, regions.json ones included, as Dynamic, so a runtime region at the same priority as the static
// dungeon loses the tie (notes/cc4-despise.md section 3).
//
// WrongLevel3: a player who dies in level 3 is moved to the cell at 5703,639,0 and, the first time, resurrected
// ("You are captured by the jailor and returned to your cell"); dying again while still listed is a normal
// death; leaving the region clears the listing. WrongJail: no harmful acts.
//
// Conversion notes: Region.AllowHarmful here takes (Mobile from, Mobile target); ServUO's takes IDamageable.
// Timer.DelayCall with a lambda replaces ServUO's; the message is LocalOverheadMessage as there.

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Network;
using Server.Regions;

namespace Server.Engines.WrongRevamp;

public static class WrongRegions
{
    public const int WrongPriority = Region.DefaultPriority + 1;

    public static readonly Rectangle2D Level3Bounds = new(5687, 623, 33, 44);
    public static readonly Rectangle2D JailBounds = new(5700, 636, 7, 7);

    public static readonly Point3D CellLocation = new(5703, 639, 0);

    private static readonly Dictionary<Map, WrongLevel3> _level3 = new();

    public static void Initialize()
    {
        Register(Map.Trammel);
        Register(Map.Felucca);
    }

    public static WrongLevel3 GetLevel3(Map map) => _level3.GetValueOrDefault(map);

    /// <summary>
    ///     Registers the two regions on a facet, under the facet's static "Wrong" region when it has one. Returns the
    ///     level-3 region. Safe to call twice: the second call returns the first registration.
    /// </summary>
    public static WrongLevel3 Register(Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        if (_level3.TryGetValue(map, out var existing) && existing?.Registered == true)
        {
            return existing;
        }

        var parent = Region.Find(CellLocation, map);

        if (parent == null || !parent.IsPartOf("Wrong"))
        {
            parent = null;
        }

        var level3 = new WrongLevel3(map, parent);
        level3.Register();

        var jail = new WrongJail(map, level3);
        jail.Register();

        _level3[map] = level3;
        return level3;
    }

    /// <summary>Test support: unregisters both regions on a facet.</summary>
    public static void Unregister(Map map)
    {
        if (_level3.Remove(map, out var level3) && level3 != null)
        {
            level3.Jail?.Unregister();
            level3.Unregister();
        }
    }
}

public class WrongLevel3 : DungeonRegion
{
    private readonly List<Mobile> _deathList = new();

    public WrongLevel3(Map map, Region parent)
        : base("Wrong Level 3", map, parent, WrongRegions.WrongPriority, ConvertTo3D(WrongRegions.Level3Bounds))
    {
    }

    public WrongJail Jail { get; internal set; }

    public IReadOnlyList<Mobile> DeathList => _deathList;

    public override void OnDeath(Mobile m)
    {
        if (m is PlayerMobile)
        {
            m.MoveToWorld(WrongRegions.CellLocation, Map);

            if (!_deathList.Contains(m))
            {
                m.Resurrect();
                _deathList.Add(m);
            }
            else
            {
                _deathList.Remove(m);
            }

            Timer.DelayCall(
                TimeSpan.FromSeconds(2),
                () => m.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1152076) // You are captured by the jailor and returned to your cell.
            );
        }

        base.OnDeath(m);
    }

    public override void OnExit(Mobile m)
    {
        if (m is PlayerMobile)
        {
            _deathList.Remove(m);
        }

        base.OnExit(m);
    }
}

public class WrongJail : DungeonRegion
{
    public WrongJail(Map map, WrongLevel3 parent)
        : base("Wrong Jail", map, parent, WrongRegions.WrongPriority + 1, ConvertTo3D(WrongRegions.JailBounds))
    {
        if (parent != null)
        {
            parent.Jail = this;
        }
    }

    public override bool AllowHarmful(Mobile from, Mobile target) => false;
}
