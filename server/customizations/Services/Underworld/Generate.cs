// ServUO: Services/Underworld/Generate.cs (cc-P197). ServUO's [DecorateSA calls this generator (and GenNavery)
// after laying its Underworld decoration; the shard has no DecorateSA, so this is its own command.
//
// [GenUnderworld (Administrator) builds the Underworld's three puzzle places on Ter Mur, in ServUO's order:
//   1. The Experimental Room: the cooldown controller at 980,1117,-42; the eight gem doors (hue 1109) and their
//      eight blockers on the thresholds at y 1116, 1102, 1090 and 1072 (x 984 and 985, z -42; ServUO gives the
//      last pair the door Room RoomTwo and the blocker Room RoomThree, copied as it is); the reward chest at
//      984,1064,-37; Sir Wilber's book at 995,1114,-36; the secret door (87/88) at 1007,1119,-42; the sign
//      (1113407) at 980,1119,-37; and the way out: a teleporter at 980,1064,-42 to 996,1117,-42 under a sparkle.
//   2. The Puzzle Room: the three puzzle boxes (west 1090,1171, east 1104,1171, north 1097,1163, z 11), the
//      instruction book at 1109,1153,-17, the two key-checking teleporters at 1097/1098,1175,0 into
//      1097/1098,1173,1, the two locked metal doors (key value 50000) at 1097/1098,1174,1 and the two teleporters
//      back out at 1097/1098,1173,1.
//   3. The Maze of Death: the puzzle box at 1068,1026,-37, the golden compass at 1070,1055,-34 and the rolled map
//      at 1072,1055,-36.
//   4. The Fountain of Fortune at 1121,957,-42 (only when none stands within five tiles: the shard's
//      ClusterFSeedFountainOfFortune places the same one, cc-P27) and the thirteen invisible tiles around its pool.
//   5. The reveal tiles: every spawnable tile of 1182,1120 11x15 at z -42 and the five at 1180,879-883.
//   6. The two cannons and their militia cannoneers at 1126/1131,1200-1203,-2 (placed only where missing, as
//      ServUO's CheckCannoneers).
//   7. Navrey Night-Eyes' lair (NavreysController.GenNavery, as [DecorateSA does), unless one already stands.
// Everything placed is tagged into the "underworld" collection and [DeleteUnderworld (ours) takes it down; the
// generator deletes its earlier run first, so it replaces rather than doubles (ServUO's doubles on a re-run; the
// D-4 rule's broken kind, as cc-P194 and cc-P196 did). ServUO tags everything of the Stygian Abyss into "sa";
// the shard has no "sa" decoration, so the key is this generator's own.
//
// One ServUO slip is corrected: its Generate adds the last ExperimentalRoomDoor to the collection a second time
// where it means the second locked MetalDoor2 (Generate.cs:159), which its delete then leaves standing; the door
// is tagged here. The two regions register themselves at startup (ExperimentalRoomRegion, MazeOfDeathRegion).

using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Quests.Haven;
using Server.Mobiles;

namespace Server.Items;

public static class GenerateUnderworldRooms
{
    public const string CollectionKey = "underworld";

    public static readonly Point3D ControllerLocation = new(980, 1117, -42);
    public static readonly Point3D ChestLocation = new(984, 1064, -37);
    public static readonly Point3D BookLocation = new(995, 1114, -36);
    public static readonly Point3D SecretDoorLocation = new(1007, 1119, -42);
    public static readonly Point3D SignLocation = new(980, 1119, -37);
    public static readonly Point3D ExitTeleporterLocation = new(980, 1064, -42);
    public static readonly Point3D ExitTeleporterDestination = new(996, 1117, -42);
    public static readonly Point3D PuzzleBookLocation = new(1109, 1153, -17);
    public static readonly Point3D MazePuzzleBoxLocation = new(1068, 1026, -37);
    public static readonly Point3D CompassLocation = new(1070, 1055, -34);
    public static readonly Point3D MapLocation = new(1072, 1055, -36);
    public static readonly Point3D FountainLocation = new(1121, 957, -42);
    public static readonly Rectangle2D RevealTileField = new(1182, 1120, 11, 15);
    public const int RevealTileFieldZ = -42;
    public const uint PuzzleRoomKeyValue = 50000;
    public const int DoorHue = 1109;

    // ServUO Generate: (the door's Room, facing, the blocker's Room, where), in its order.
    public static readonly (Room DoorRoom, DoorFacing Facing, Room BlockerRoom, Point3D At)[] Doors =
    {
        (Room.RoomZero, DoorFacing.WestCCW, Room.RoomZero, new Point3D(984, 1116, -42)),
        (Room.RoomZero, DoorFacing.EastCW, Room.RoomZero, new Point3D(985, 1116, -42)),
        (Room.RoomOne, DoorFacing.WestCCW, Room.RoomOne, new Point3D(984, 1102, -42)),
        (Room.RoomOne, DoorFacing.EastCW, Room.RoomOne, new Point3D(985, 1102, -42)),
        (Room.RoomTwo, DoorFacing.WestCCW, Room.RoomTwo, new Point3D(984, 1090, -42)),
        (Room.RoomTwo, DoorFacing.EastCW, Room.RoomTwo, new Point3D(985, 1090, -42)),
        (Room.RoomTwo, DoorFacing.WestCCW, Room.RoomThree, new Point3D(984, 1072, -42)), // ServUO: RoomTwo door, RoomThree blocker
        (Room.RoomTwo, DoorFacing.EastCW, Room.RoomThree, new Point3D(985, 1072, -42))
    };

    public static readonly (PuzzleType Type, Point3D At)[] PuzzleBoxes =
    {
        (PuzzleType.WestBox, new Point3D(1090, 1171, 11)),
        (PuzzleType.EastBox, new Point3D(1104, 1171, 11)),
        (PuzzleType.NorthBox, new Point3D(1097, 1163, 11))
    };

    // The key-checking teleporters into the Puzzle Room: (where, destination).
    public static readonly (Point3D At, Point3D Dest)[] PuzzleRoomTeleporters =
    {
        (new Point3D(1097, 1175, 0), new Point3D(1097, 1173, 1)),
        (new Point3D(1098, 1175, 0), new Point3D(1098, 1173, 1))
    };

    public static readonly (DoorFacing Facing, Point3D At)[] PuzzleRoomDoors =
    {
        (DoorFacing.WestCCW, new Point3D(1097, 1174, 1)),
        (DoorFacing.EastCW, new Point3D(1098, 1174, 1))
    };

    // Plain teleporters: the two out of the Puzzle Room and the one out of the Experimental Room's last room.
    public static readonly (Point3D At, Point3D Dest)[] Teleporters =
    {
        (new Point3D(1097, 1173, 1), new Point3D(1097, 1175, 0)),
        (new Point3D(1098, 1173, 1), new Point3D(1098, 1175, 0)),
        (ExitTeleporterLocation, ExitTeleporterDestination)
    };

    public static readonly Point3D[] InvisibleTiles =
    {
        new(1121, 965, -41),
        new(1122, 965, -40),
        new(1123, 965, -41),
        new(1124, 965, -41),
        new(1122, 964, -41),
        new(1123, 964, -41),
        new(1123, 963, -40),
        new(1123, 962, -40),
        new(1123, 961, -41),
        new(1122, 961, -41),
        new(1122, 960, -41),
        new(1121, 960, -41),
        new(1121, 959, -41)
    };

    public static readonly Point3D[] PassageRevealTiles =
    {
        new(1180, 883, 0),
        new(1180, 882, 0),
        new(1180, 881, 0),
        new(1180, 880, 0),
        new(1180, 879, 0)
    };

    // (the cannon, its cannoneer), as ServUO's CheckCannoneers.
    public static readonly (Point3D Cannon, Point3D Cannoneer)[] Cannons =
    {
        (new Point3D(1126, 1200, -2), new Point3D(1126, 1203, -2)),
        (new Point3D(1131, 1200, -2), new Point3D(1131, 1203, -2))
    };

    public static void Initialize()
    {
        CommandSystem.Register("GenUnderworld", AccessLevel.Administrator, Generate_OnCommand);
        CommandSystem.Register("DeleteUnderworld", AccessLevel.Administrator, Delete_OnCommand);
    }

    [Usage("GenUnderworld")]
    [Description("Builds the Underworld's Experimental Room, Puzzle Room, Maze of Death and Navrey's lair on Ter Mur.")]
    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Replaces, Shard = CommandShard.Any, NoDryRun = true, Summary = "The Ter Mur Underworld's puzzle places: the Experimental Room (doors, blockers, chest, book, sign), the Puzzle Room (boxes, key, doors, teleporters), the Maze of Death (puzzle box, compass, map), the Fountain of Fortune if missing, the reveal tiles, the cannons and Navrey Night-Eyes' lair. Deletes its earlier placement first, so it can run again.")]
    public static void Generate_OnCommand(CommandEventArgs e)
    {
        var deleted = Delete();

        if (deleted > 0)
        {
            e.Mobile.SendMessage($"{deleted} objects of an earlier run deleted.");
        }

        e.Mobile.SendMessage(Generate(e.Mobile));
        e.Mobile.SendMessage("Experimental Room, Puzzle Room and Maze of Death initialized.");
    }

    [Usage("DeleteUnderworld")]
    [Description("Removes everything GenUnderworld placed.")]
    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true, Summary = "DELETES everything GenUnderworld placed (the three puzzle places, the fountain it placed, the reveal tiles, the cannons and the lair).")]
    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = Delete();
        e.Mobile.SendMessage($"Underworld rooms removed ({deleted} objects).");
    }

    public static int Delete() => WeakEntityCollection.Delete(CollectionKey);

    private static T Place<T>(T item, Point3D at) where T : Item
    {
        WeakEntityCollection.Add(CollectionKey, item);
        item.MoveToWorld(at, Map.TerMur);
        return item;
    }

    /// <summary>ServUO's Generate and GenNavery. Returns a one-line report.</summary>
    public static string Generate(Mobile m = null)
    {
        var map = Map.TerMur;

        // The Experimental Room.
        Place(new ExperimentalRoomController(), ControllerLocation);

        foreach (var (doorRoom, facing, blockerRoom, at) in Doors)
        {
            var door = new ExperimentalRoomDoor(doorRoom, facing) { Hue = DoorHue };
            Place(door, at);
            Place(new ExperimentalRoomBlocker(blockerRoom), at);
        }

        Place(new ExperimentalRoomChest(), ChestLocation);

        var instr = new ExperimentalBook { Movable = false };
        Place(instr, BookLocation);

        var dd = new SecretDungeonDoor(DoorFacing.NorthCCW) { ClosedId = 87, OpenedId = 88 };
        Place(dd, SecretDoorLocation);

        var sign = new LocalizedSign(3026, 1113407) { Movable = false }; // Experimental Room Access
        Place(sign, SignLocation);

        // The Puzzle Room.
        foreach (var (type, at) in PuzzleBoxes)
        {
            Place(new PuzzleBox(type), at);
        }

        Place(new PuzzleBook { Movable = false }, PuzzleBookLocation);
        Place(new MagicKey(), PuzzleBookLocation);

        foreach (var (at, dest) in PuzzleRoomTeleporters)
        {
            var tele = new PuzzleRoomTeleporter { PointDest = dest, MapDest = map };
            Place(tele, at);
        }

        foreach (var (facing, at) in PuzzleRoomDoors)
        {
            var door2 = new MetalDoor2(facing) { Locked = true, KeyValue = PuzzleRoomKeyValue };
            Place(door2, at);
        }

        foreach (var (at, dest) in Teleporters)
        {
            Place(new Teleporter(dest, map), at);
        }

        Place(new Static(14138), ExitTeleporterLocation);

        // The Maze of Death.
        Place(new UnderworldPuzzleBox(), MazePuzzleBoxLocation);
        Place(new GoldenCompass(), CompassLocation);
        Place(new RolledMapOfTheUnderworld { Movable = false }, MapLocation);

        // The Fountain of Fortune, unless the shard's seeder (or an earlier run) already placed it.
        var fountainPlaced = false;

        if (FindFountain() == null)
        {
            Place(new FountainOfFortune(), FountainLocation);
            fountainPlaced = true;
        }

        foreach (var p in InvisibleTiles)
        {
            Place(new InvisibleTile(), p);
        }

        var reveal = GenerateRevealTiles();
        var cannons = CheckCannoneers();

        // Navrey Night-Eyes' lair, as [DecorateSA does.
        var lairBuilt = false;

        if (NavreysController.Find() == null)
        {
            NavreysController.GenNavery(m);
            var controller = NavreysController.Find();

            if (controller != null)
            {
                WeakEntityCollection.Add(CollectionKey, controller);
                lairBuilt = true;
            }
        }

        return $"Underworld: {Doors.Length} gem doors and blockers, the chest, two books, the secret door, the sign, " +
               $"{PuzzleBoxes.Length} puzzle boxes, the magic key, {PuzzleRoomTeleporters.Length + Teleporters.Length} teleporters, " +
               $"{PuzzleRoomDoors.Length} locked doors, the maze box, compass and map, " +
               $"{(fountainPlaced ? "the fountain" : "no fountain (one stands)")}, {InvisibleTiles.Length} invisible tiles, " +
               $"{reveal} reveal tiles, {cannons} cannons placed, {(lairBuilt ? "Navrey's lair" : "the lair already stood")}.";
    }

    public static FountainOfFortune FindFountain()
    {
        foreach (var item in Map.TerMur.GetItemsInRange(FountainLocation, ClusterFFountainOfFortuneSeeder.SearchRange))
        {
            if (item is FountainOfFortune { Deleted: false } fountain)
            {
                return fountain;
            }
        }

        return null;
    }

    /// <summary>ServUO's GenerateRevealTiles. Returns how many were placed.</summary>
    public static int GenerateRevealTiles()
    {
        var map = Map.TerMur;
        var placed = 0;

        for (var x = RevealTileField.X; x <= RevealTileField.X + RevealTileField.Width - 1; x++)
        {
            for (var y = RevealTileField.Y; y <= RevealTileField.Y + RevealTileField.Height - 1; y++)
            {
                if (map != null && map.CanSpawnMobile(x, y, RevealTileFieldZ))
                {
                    Place(new RevealTile(), new Point3D(x, y, RevealTileFieldZ));
                    placed++;
                }
            }
        }

        foreach (var p in PassageRevealTiles)
        {
            Place(new RevealTile(), p);
            placed++;
        }

        return placed;
    }

    /// <summary>ServUO's CheckCannoneers. Returns how many cannons were placed (0 to 2).</summary>
    public static int CheckCannoneers()
    {
        var map = Map.TerMur;
        var placed = 0;

        foreach (var (cannonAt, cannoneerAt) in Cannons)
        {
            Cannon cannon = null;

            foreach (var item in map.GetItemsInRange(cannonAt, 0))
            {
                if (item is Cannon { Deleted: false } c)
                {
                    cannon = c;
                    break;
                }
            }

            if (cannon == null)
            {
                cannon = new Cannon(CannonDirection.North);
                Place(cannon, cannonAt);
                placed++;
            }

            MilitiaCanoneer cannoneer = null;

            foreach (var mob in map.GetMobilesInRange(cannoneerAt, 0))
            {
                if (mob is MilitiaCanoneer { Deleted: false } mc)
                {
                    cannoneer = mc;
                    break;
                }
            }

            if (cannoneer == null)
            {
                cannoneer = new MilitiaCanoneer();
                WeakEntityCollection.Add(CollectionKey, cannoneer);
                cannoneer.MoveToWorld(cannoneerAt, map);
            }

            cannon.Canoneer = cannoneer;
        }

        return placed;
    }
}
