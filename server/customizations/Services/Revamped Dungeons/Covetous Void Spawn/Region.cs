// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Region.cs (cc-P194).
//
// The Void Pool region, a DYNAMIC region the controller registers over the bottom of Covetous (5383,1960 236x80
// and 5429,1948 12x10) on its facet. It carries the battle's messages to every player inside, credits the controller
// for every kill while a battle is on (and rolls the void artifact: 0.1% a kill, rising a tenth of a percent per
// wave past ten, to a random damager), refuses to open a void spawn's corpse ("There is no loot on the corpse"),
// forces dungeon light, blocks the stuck menu, and blocks recall, gate and mark in (travel out is allowed).
//
// Priority is DefaultPriority + 1, the Despise rule (notes/cc4-despise.md section 3): the static "Covetous"
// DungeonRegion of regions.json covers the same tiles at 50, and pinned constructs every region as Dynamic, so
// the tie at 50 would go to the static one.
//
// Conversion notes: GetEnumeratedMobiles is GetMobiles (a List<Mobile>); SendLocalizedMessage(number, args, hue)
// is pinned's (number, ReadOnlySpan<char> args, hue); CheckTravel is BaseRegion's with the out message;
// Corpse.Owner is the generated property.

using System;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Server.Engines.VoidPool;

public class VoidPoolRegion : BaseRegion
{
    public const int VoidPoolPriority = DefaultPriority + 1;

    public static readonly Rectangle2D[] Bounds =
    {
        new(5383, 1960, 236, 80),
        new(5429, 1948, 12, 10)
    };

    public VoidPoolRegion(VoidPoolController controller, Map map) : base("Void Pool", map, VoidPoolPriority, Bounds) =>
        Controller = controller;

    public VoidPoolController Controller { get; }

    public void SendRegionMessage(int localization)
    {
        foreach (var m in GetPlayers())
        {
            m.SendLocalizedMessage(localization);
        }
    }

    public void SendRegionMessage(int localization, int hue)
    {
        foreach (var m in GetPlayers())
        {
            m.SendLocalizedMessage(localization, "", hue);
        }
    }

    public void SendRegionMessage(int localization, string args)
    {
        foreach (var m in GetPlayers())
        {
            m.SendLocalizedMessage(localization, args);
        }
    }

    public void SendRegionMessage(string message)
    {
        foreach (var m in GetPlayers())
        {
            m.SendMessage(0x25, message);
        }
    }

    public override void OnDeath(Mobile m)
    {
        if (m is BaseCreature { Controlled: false, Summoned: false } bc && Controller?.OnGoing == true)
        {
            Controller.OnCreatureKilled(bc);

            if (bc is CovetousCreature { VoidSpawn: true } cc)
            {
                var wave = cc.Level;
                var bump = wave > 10 ? Math.Min(60, wave - 10) / 1000.0 : 0;
                var chance = 0.001 + bump;

                if (chance > Utility.RandomDouble())
                {
                    var mob = DemonKnight.FindRandomPlayer(bc);

                    if (mob != null)
                    {
                        var artifact = VoidPoolRewards.DropRandomArtifact();

                        if (artifact != null)
                        {
                            var pack = mob.Backpack;

                            if (pack == null || !pack.TryDropItem(mob, artifact, false))
                            {
                                mob.BankBox.DropItem(artifact);
                            }

                            mob.SendLocalizedMessage(1062317); // For your valor in combating the fallen beast, a special artifact has been bestowed on you.
                        }
                    }
                }
            }
        }

        base.OnDeath(m);
    }

    public override bool OnDoubleClick(Mobile m, object o)
    {
        if (o is Corpse c && m.AccessLevel == AccessLevel.Player)
        {
            if (c.Owner == null || c.Owner is CovetousCreature { VoidSpawn: true })
            {
                c.LabelTo(m, 1152684); // There is no loot on the corpse.
                return false;
            }
        }

        return base.OnDoubleClick(m, o);
    }

    public override void AlterLightLevel(Mobile m, ref int global, ref int personal)
    {
        global = LightCycle.DungeonLevel;
    }

    public override bool CanUseStuckMenu(Mobile m) => false;

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        return travelType switch
        {
            TravelCheckType.RecallTo => false,
            TravelCheckType.GateTo   => false,
            TravelCheckType.Mark     => false,
            _                        => true
        };
    }
}
