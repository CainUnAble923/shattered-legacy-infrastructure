// ServUO: Data/Decoration/Wrong/WrongMaze.cfg (cc-P194) - the 290 decoration entries ServUO's Generate.cs places
// with Decorate.Generate("wrong", "Data/Decoration/Wrong", Map.Trammel, Map.Felucca): the level-3 maze (224
// invisible WrongMazeWall pieces that show themselves when someone stands next to them, 34 lockpickable
// WrongBarredMetalDoors, one stock BarredMetalDoor, 31 statics: dungeon walls and the sparkle at the two
// teleporters' destinations). Carried as data because apply-patches.sh installs .cs files only (the cfg would
// need a data patch), in file order; every value is the cfg's.
//
// Placement follows pinned Decorate (Commands/Object Creation/Decorate.cs): a door is built with its Facing and
// then given the cfg's item id (:1057-1059), a Static with its id, anything else with its own constructor and then
// the id; every piece is Movable = false, and a tile that already holds an item of that id at that z is skipped
// (:1203-1206), so the command can run twice without doubling the maze.

using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.WrongRevamp;

public static class WrongDecoration
{
    public readonly record struct Piece(string Type, int ItemID, int X, int Y, int Z, DoorFacing? Facing);

    // Generated from Data/Decoration/Wrong/WrongMaze.cfg; columns as in Piece.
    public static readonly Piece[] Pieces =
    {
        new("WrongMazeWall", 0x0248, 5697, 639, 0, null),
        new("WrongMazeWall", 0x0248, 5697, 640, 0, null),
        new("Static", 0x0242, 5700, 637, 0, null),
        new("Static", 0x0242, 5700, 638, 0, null),
        new("Static", 0x0242, 5700, 639, 0, null),
        new("Static", 0x0242, 5700, 640, 0, null),
        new("Static", 0x0242, 5700, 641, 0, null),
        new("Static", 0x0242, 5700, 642, 0, null),
        new("Static", 0x0242, 5706, 642, 0, null),
        new("Static", 0x0242, 5706, 641, 0, null),
        new("Static", 0x0242, 5706, 640, 0, null),
        new("Static", 0x0242, 5706, 638, 0, null),
        new("Static", 0x0242, 5706, 637, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 638, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 639, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 640, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 639, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 640, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 639, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 640, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 639, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 640, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 639, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 640, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 641, 0, null),
        new("WrongMazeWall", 0x0242, 5701, 626, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 641, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 642, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 643, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 644, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 645, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 642, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 634, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 635, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 636, 0, null),
        new("WrongMazeWall", 0x0242, 5709, 637, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 634, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 635, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 636, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 637, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 643, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 644, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 645, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 643, 0, null),
        new("WrongMazeWall", 0x0242, 5697, 642, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 644, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 645, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 646, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 647, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 648, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 648, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 631, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 632, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 633, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 634, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 635, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 636, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 637, 0, null),
        new("WrongMazeWall", 0x0242, 5694, 638, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 631, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 632, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 633, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 634, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 635, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 636, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 637, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 638, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 641, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 642, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 643, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 644, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 645, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 646, 0, null),
        new("WrongMazeWall", 0x0242, 5712, 647, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 641, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 642, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 643, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 644, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 645, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 646, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 647, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 648, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 649, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 650, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 651, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 628, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 629, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 630, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 631, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 632, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 633, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 634, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 635, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 636, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 637, 0, null),
        new("WrongMazeWall", 0x0242, 5715, 638, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 628, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 629, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 630, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 631, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 632, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 633, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 634, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 635, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 636, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 637, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 638, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 641, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 642, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 643, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 644, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 645, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 646, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 647, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 648, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 649, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 650, 0, null),
        new("WrongMazeWall", 0x0242, 5691, 651, 0, null),
        new("WrongMazeWall", 0x0242, 5702, 655, 0, null),
        new("WrongMazeWall", 0x0242, 5702, 654, 0, null),
        new("WrongMazeWall", 0x0242, 5702, 652, 0, null),
        new("WrongMazeWall", 0x0242, 5702, 653, 0, null),
        new("WrongMazeWall", 0x0242, 5701, 624, 0, null),
        new("WrongMazeWall", 0x0242, 5701, 625, 0, null),
        new("WrongMazeWall", 0x0242, 5701, 627, 0, null),
        new("Static", 0x0241, 5706, 642, 0, null),
        new("Static", 0x0241, 5705, 642, 0, null),
        new("Static", 0x0241, 5704, 642, 0, null),
        new("Static", 0x0241, 5703, 642, 0, null),
        new("Static", 0x0241, 5702, 642, 0, null),
        new("Static", 0x0241, 5701, 642, 0, null),
        new("Static", 0x0241, 5706, 636, 0, null),
        new("Static", 0x0241, 5704, 636, 0, null),
        new("Static", 0x0241, 5705, 636, 0, null),
        new("Static", 0x0241, 5703, 636, 0, null),
        new("Static", 0x0241, 5702, 636, 0, null),
        new("Static", 0x0241, 5701, 636, 0, null),
        new("WrongMazeWall", 0x0241, 5701, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5702, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5701, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5702, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5703, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5703, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5704, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5701, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5702, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5700, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5699, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5698, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5703, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5704, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5705, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5706, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5707, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5708, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5709, 633, 0, null),
        new("WrongMazeWall", 0x0241, 5698, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5699, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5700, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5701, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5702, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5705, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5706, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5707, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5708, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5709, 645, 0, null),
        new("WrongMazeWall", 0x0241, 5695, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5696, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5697, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5698, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5699, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5692, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5693, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5694, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5695, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5696, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5697, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5698, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5699, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5700, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5703, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5704, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5705, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5706, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5707, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5708, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5709, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5710, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5711, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5712, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5713, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5714, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5715, 651, 0, null),
        new("WrongMazeWall", 0x0241, 5704, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5705, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5706, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5707, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5708, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5709, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5710, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5711, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5712, 648, 0, null),
        new("WrongMazeWall", 0x0241, 5703, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5704, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5705, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5706, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5707, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5708, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5709, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5710, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5711, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5712, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5695, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5696, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5697, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5698, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5699, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5700, 630, 0, null),
        new("WrongMazeWall", 0x0241, 5692, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5693, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5694, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5699, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5700, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5701, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5704, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5705, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5706, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5707, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5708, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5713, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5714, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5715, 627, 0, null),
        new("WrongMazeWall", 0x0241, 5702, 627, 0, null),
        new("WrongMazeWall", 0x024A, 5697, 641, 0, null),
        new("WrongMazeWall", 0x0246, 5697, 638, 0, null),
        new("WrongMazeWall", 0x0247, 5702, 648, 0, null),
        new("WrongMazeWall", 0x0247, 5701, 648, 0, null),
        new("WrongMazeWall", 0x0247, 5696, 627, 0, null),
        new("WrongMazeWall", 0x0247, 5697, 627, 0, null),
        new("WrongMazeWall", 0x0247, 5710, 627, 0, null),
        new("WrongMazeWall", 0x0247, 5711, 627, 0, null),
        new("WrongMazeWall", 0x0245, 5700, 648, 0, null),
        new("WrongMazeWall", 0x0245, 5695, 627, 0, null),
        new("WrongMazeWall", 0x0245, 5709, 627, 0, null),
        new("WrongMazeWall", 0x0249, 5703, 648, 0, null),
        new("WrongMazeWall", 0x0249, 5698, 627, 0, null),
        new("WrongMazeWall", 0x0249, 5712, 627, 0, null),
        new("BarredMetalDoor", 0x068F, 5706, 639, 0, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x068D, 5797, 595, 10, DoorFacing.SouthCCW),
        new("WrongBarredMetalDoor", 0x068D, 5797, 588, 10, DoorFacing.SouthCCW),
        new("WrongBarredMetalDoor", 0x068D, 5797, 580, 10, DoorFacing.SouthCCW),
        new("WrongBarredMetalDoor", 0x068D, 5797, 572, 10, DoorFacing.SouthCCW),
        new("WrongBarredMetalDoor", 0x068D, 5797, 564, 10, DoorFacing.SouthCCW),
        new("WrongBarredMetalDoor", 0x068D, 5797, 556, 10, DoorFacing.SouthCCW),
        new("WrongBarredMetalDoor", 0x068F, 5797, 594, 10, DoorFacing.NorthCW),
        new("WrongBarredMetalDoor", 0x068F, 5797, 587, 10, DoorFacing.NorthCW),
        new("WrongBarredMetalDoor", 0x068F, 5797, 579, 10, DoorFacing.NorthCW),
        new("WrongBarredMetalDoor", 0x068F, 5797, 571, 10, DoorFacing.NorthCW),
        new("WrongBarredMetalDoor", 0x068F, 5797, 563, 10, DoorFacing.NorthCW),
        new("WrongBarredMetalDoor", 0x068F, 5797, 555, 10, DoorFacing.NorthCW),
        new("WrongBarredMetalDoor", 0x0691, 5848, 581, 14, DoorFacing.SouthCW),
        new("WrongBarredMetalDoor", 0x0691, 5857, 595, 15, DoorFacing.SouthCW),
        new("WrongBarredMetalDoor", 0x0691, 5857, 587, 15, DoorFacing.SouthCW),
        new("WrongBarredMetalDoor", 0x0691, 5857, 579, 15, DoorFacing.SouthCW),
        new("WrongBarredMetalDoor", 0x0691, 5857, 571, 15, DoorFacing.SouthCW),
        new("WrongBarredMetalDoor", 0x0691, 5857, 563, 15, DoorFacing.SouthCW),
        new("WrongBarredMetalDoor", 0x0691, 5863, 555, 15, DoorFacing.SouthCW),
        new("WrongBarredMetalDoor", 0x0693, 5848, 580, 14, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x0693, 5857, 594, 15, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x0693, 5857, 586, 15, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x0693, 5857, 578, 15, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x0693, 5857, 570, 15, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x0693, 5857, 562, 15, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x0693, 5863, 554, 15, DoorFacing.NorthCCW),
        new("WrongBarredMetalDoor", 0x0685, 5652, 567, 20, DoorFacing.WestCCW),
        new("WrongBarredMetalDoor", 0x0685, 5659, 567, 20, DoorFacing.WestCCW),
        new("WrongBarredMetalDoor", 0x0685, 5666, 567, 20, DoorFacing.WestCCW),
        new("WrongBarredMetalDoor", 0x0685, 5726, 567, 20, DoorFacing.WestCCW),
        new("WrongBarredMetalDoor", 0x0687, 5653, 567, 20, DoorFacing.EastCCW),
        new("WrongBarredMetalDoor", 0x0687, 5660, 567, 20, DoorFacing.EastCCW),
        new("WrongBarredMetalDoor", 0x0687, 5667, 567, 20, DoorFacing.EastCCW),
        new("WrongBarredMetalDoor", 0x0687, 5727, 567, 20, DoorFacing.EastCCW),
        new("Static", 0x031A, 5652, 552, 20, null),
        new("Static", 0x375A, 5690, 573, 25, null),
        new("Static", 0x375A, 5827, 590, 1, null),
        new("Static", 0x375A, 5872, 532, 24, null),
        new("Static", 0x375A, 5792, 525, 10, null),
        new("Static", 0x375A, 5698, 662, 1, null),
        new("Static", 0x375A, 5708, 625, 1, null),
        new("Static", 0x375A, 5732, 554, 24, null),
    };

    /// <summary>Places every piece on the facet that is not already there. Returns the pieces placed.</summary>
    public static List<Item> Generate(Map map)
    {
        var placed = new List<Item>();

        foreach (var piece in Pieces)
        {
            if (Exists(map, piece))
            {
                continue;
            }

            var item = Construct(piece);

            if (item == null)
            {
                continue;
            }

            item.Movable = false;
            WeakEntityCollection.Add(WrongGenerator.CollectionKey, item);
            item.MoveToWorld(new Point3D(piece.X, piece.Y, piece.Z), map);
            placed.Add(item);
        }

        return placed;
    }

    public static Item Construct(Piece piece)
    {
        Item item = piece.Type switch
        {
            "Static"               => new Static(piece.ItemID),
            "WrongMazeWall"        => new WrongMazeWall(),
            "WrongBarredMetalDoor" => new WrongBarredMetalDoor(piece.Facing ?? DoorFacing.WestCW),
            "BarredMetalDoor"      => new BarredMetalDoor(piece.Facing ?? DoorFacing.WestCW),
            _                      => null
        };

        if (item != null && piece.ItemID > 0)
        {
            item.ItemID = piece.ItemID;
        }

        return item;
    }

    public static bool Exists(Map map, Piece piece)
    {
        foreach (var item in map.GetItemsAt(piece.X, piece.Y))
        {
            if (item.Z == piece.Z && item.ItemID == piece.ItemID && !item.Deleted)
            {
                return true;
            }
        }

        return false;
    }
}
