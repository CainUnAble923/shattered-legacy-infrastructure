// cc-P742 (Chase 10-30: "Could we teleport during vision to a new place that is more in the story line of a gem on a
// pedestal in a dungeon and the stranger then breaks it?"): the loop vision plays in a small dungeon shrine instead of
// where the player stands. Notes: shard-migration notes/cc-P742-isle-vision-chamber.md; pieces: placed.md beside it.
//
// Where: the empty part of site A on the player's own lesson map (the Fold, map 32, or their private copy), north of
// Blackthorn's Fold dungeon (BlackthornFold.Dungeon, y 3804 and south) and west of the Wakening Isle (x 6594 and east).
// That strip (x 6160-6590, y 3744-3803) is void land (0x0244, Z 0) on both stock Trammel and the Siege of Sosaria file,
// which no walk, boat, spell or gate reaches (FoldRegion refuses every travel type), so the room needs no door.
//
// How: the room is placed per scene from TutorialVisionChamberLayout (114 pieces, all TutorialVisionProp, never kept by
// a save) when the vision starts, and taken away when it ends. One slot per scene on a map, 28 tiles apart so no two
// scenes are in each other's view (18 tiles): on a private copy that is always slot 0; on the shared Fold the first free
// slot. The pieces' serials go into TutorialVisionStore with the scene, so a restart mid-scene removes them too.

#nullable enable

using System.Collections.Generic;
using Server.Items;

namespace Server.Tutorial;

public static class TutorialVisionChamber
{
    /// <summary>Slot 0's north-west corner (the wall post), and the floor's height there (void land is at Z 0).</summary>
    public const int FirstX = 6170;
    public const int BaseY = 3752;
    public const int FloorZ = 0;

    /// <summary>Tiles between slots: the next room's nearest wall is 22 tiles from the onlooker, past a client's view (18).</summary>
    public const int Spacing = 28;

    /// <summary>Slots on one map: corners x 6170 to 6562, rooms ending by x 6570, all west of the isle (6594).</summary>
    public const int Slots = 15;

    /// <summary>The pedestal's height (tiledata 0x1223), where the server has no tiledata loaded (the test host).</summary>
    public const int PedestalHeight = 9;

    /// <summary>The wall box is 0..Size-1 on both axes.</summary>
    public const int Size = 9;

    /// <summary>The whole strip the slots use, for the login check (Rectangle2D's end is exclusive).</summary>
    public static readonly Rectangle2D Strip = new(new Point2D(FirstX, BaseY), new Point2D(FirstX + Spacing * (Slots - 1) + Size, BaseY + Size));

    // The scene's spots, relative to the corner (placed.md).
    public static readonly Point2D Pedestal = new(4, 4);
    public static readonly Point2D Onlooker = new(6, 6);      // the player, hidden, facing the pedestal (north-west)
    public static readonly Point2D[] StrangerWalk = [new(1, 1), new(2, 2), new(3, 3)]; // from the dark corner to the Gem
    public static readonly Point2D Echo = new(5, 5);
    public static readonly Point2D Hooded = new(2, 2);
    public static readonly Point2D BrokenSword = new(3, 4);
    public static readonly Point2D SwordTip = new(2, 5);
    public static readonly Point2D[] Shards = [new(3, 5), new(5, 3), new(5, 5), new(3, 2), new(6, 5)];

    public const int PedestalId = 0x1223;
    public const int BrazierId = 0x0E31;
    public const int NorthTorchId = 0x0A0C;
    public const int WestTorchId = 0x0A07;

    /// <summary>Slot <paramref name="slot"/>'s corner.</summary>
    public static Point3D Corner(int slot) => new(FirstX + Spacing * slot, BaseY, FloorZ);

    /// <summary>A spot in slot <paramref name="slot"/>'s room, at the floor.</summary>
    public static Point3D At(int slot, Point2D p) => new(FirstX + Spacing * slot + p.X, BaseY + p.Y, FloorZ);

    /// <summary>Whether a point lies in the chamber strip of a Fold map (anyone found there outside a scene is sent back).</summary>
    public static bool Contains(Point3D p, Map? map) => FoldMaps.IsFold(map) && Strip.Contains(new Point2D(p.X, p.Y));

    /// <summary>The lowest slot no other scene on <paramref name="map"/> uses; -1 when every slot is busy.</summary>
    public static int FreeSlot(Map map, IEnumerable<int> used)
    {
        var taken = new HashSet<int>(used);
        for (var slot = 0; slot < Slots; slot++)
        {
            if (!taken.Contains(slot)) { return slot; }
        }

        return -1;
    }

    /// <summary>Places the room's pieces in slot <paramref name="slot"/> of <paramref name="map"/>; returns them, the pedestal last.</summary>
    public static List<Item> Build(Map map, int slot)
    {
        var placed = new List<Item>(TutorialVisionChamberLayout.Pieces.Length);
        Item? pedestal = null;
        foreach (var row in TutorialVisionChamberLayout.Pieces)
        {
            var id = row[0];
            var item = new TutorialVisionProp(id);
            switch (id)
            {
                case BrazierId:
                    item.Name = "a brazier";
                    item.Light = LightType.Circle300;
                    break;
                case NorthTorchId or WestTorchId:
                    item.Light = LightType.Circle225;
                    break;
                case PedestalId:
                    item.Name = "a pedestal";
                    pedestal = item;
                    break;
            }

            item.MoveToWorld(new Point3D(FirstX + Spacing * slot + row[1], BaseY + row[2], FloorZ + row[3]), map);
            placed.Add(item);
        }

        if (pedestal != null)
        {
            placed.Remove(pedestal);
            placed.Add(pedestal);
        }

        return placed;
    }
}
