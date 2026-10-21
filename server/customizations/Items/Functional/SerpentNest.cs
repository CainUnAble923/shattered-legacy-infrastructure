// cc-P199: ServUO pub57 Scripts/Items/Functional/SerpentNest.cs. "a serpent's nest" (art 0x2233, hue 0x456,
// immovable), the Stygian Abyss nest a player reaches into from one tile away: 2.5% finds a RareSerpentEgg (into the
// pack); otherwise one of three: the hole is too deep, the eggs are smashed and the nest collapses, or the eggs hatch
// four snakes (lava snake, snake, coral snake or giant serpent) around the nest, the nest collapses and the player's
// hand is stuck (frozen) for five seconds. ServUO places the nests through its XML spawner, one per spawner beside four
// giant serpents, outside Medusa's lair (Spawns/termur.xml: MedusaSNest centre 795,771, MedusaSNest1 775,770,
// MedusaSNest2 778,732, MedusaSNest3 806,734, all TerMur); nothing here places one.
//
// Differences from ServUO:
//   - The OnMoveOver branch (SerpentNest.cs:115-175), a snake charmed by a player searching the nest, is left out.
//     It reads BaseCreature.CharmMaster and calls EndCharm(), ServUO's snake-charming flute system, which neither
//     pinned nor this shard has; no creature here can be charmed, so the branch could never run. The base OnMoveOver
//     (true) is ServUO's result for every other mover.
//   - snake.RemoveOnSave = true (:94): pinned's Mobile has no RemoveOnSave, so hatched snakes are saved like any
//     other creature. They are ordinary wild snakes either way.
//   - Map.GetSpawnPosition(Location, 1) is ServUO's Map.cs:1159-1175 (ten random tries within the range, at the
//     nest's Z or the ground's, else the nest itself), written here as a private helper: pinned's Map has no
//     GetSpawnPosition. Map.CanSpawnMobile and GetAverageZ are pinned's (Server/Maps/Map.cs:237, 1102).
//   - Timer.DelayCall with a TimerCallback is Timer.StartTimer.

using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SerpentNest : Item
{
    private static readonly Type[] _snakeTypes =
    {
        typeof(LavaSnake), typeof(Snake),
        typeof(CoralSnake), typeof(GiantSerpent)
    };

    [Constructible]
    public SerpentNest() : base(0x2233)
    {
        Hue = 0x456;
        Movable = false;
    }

    public override int LabelNumber => 1112582; // a serpent's nest

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(this, 1))
        {
            SendLocalizedMessageTo(from, 1076766); // That is too far away.
            return;
        }

        from.RevealingAction();

        if (0.025 > Utility.RandomDouble())
        {
            from.AddToBackpack(new RareSerpentEgg());
            from.SendLocalizedMessage(1112581); // You reach in and find a rare serpent egg!!
        }
        else
        {
            switch (Utility.Random(3))
            {
                case 0:
                    {
                        from.SendLocalizedMessage(1112578); // You try to reach the eggs, but the hole is too deep.

                        break;
                    }
                case 1:
                    {
                        from.SendLocalizedMessage(1112579); // You reach in but clumsily destroy the eggs inside the nest.
                        Collapse(from);

                        break;
                    }
                case 2:
                    {
                        from.SendLocalizedMessage(1112580); // Beware! You've hatched the eggs!!
                        HatchEggs(from);

                        from.PrivateOverheadMessage(MessageType.Regular, 33, 1112940, from.NetState); // Your hand remains stuck!!!
                        from.Frozen = true;

                        Timer.StartTimer(
                            TimeSpan.FromSeconds(5.0),
                            () =>
                            {
                                from.Frozen = false;
                                from.PrivateOverheadMessage(MessageType.Regular, 65, 1112941, from.NetState); // You manage to free your hand!
                            }
                        );

                        break;
                    }
            }
        }
    }

    public void Collapse(Mobile from)
    {
        from.SendLocalizedMessage(1112583); // The nest collapses.

        Delete();
    }

    public void HatchEggs(Mobile from)
    {
        from.SendLocalizedMessage(1112577); // A swarm of snakes springs forth from the nest and attacks you!!!

        for (var i = 0; i < 4; i++)
        {
            try
            {
                var snake = (BaseCreature)Activator.CreateInstance(_snakeTypes[Utility.Random(_snakeTypes.Length)]);

                snake.MoveToWorld(GetSpawnPosition(Map, Location, 1), Map);
            }
            catch
            {
                // ServUO swallows a failed spawn the same way.
            }
        }

        Collapse(from);
    }

    // ServUO Server/Map.cs:1159-1175, Map.GetSpawnPosition(Point3D center, int range).
    private static Point3D GetSpawnPosition(Map map, Point3D center, int range)
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
}
