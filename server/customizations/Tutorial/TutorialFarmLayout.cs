// cc-P276: the tutorial farm's pieces, generated from the approved v3 layout (Chase, 2026-10-04 "looks good") by
// shard-migration notes/cc-P276-tools/gen_farm_layout.py from design/tutorial-farm/west_v3_layout.json. Do not edit by
// hand: change the layout and run the generator. Every piece is stock art at its tile and Z, absolute Trammel
// coordinates; Z is the client land the mockup was drawn on (the farm sits on unpatched land: P276's map patch only
// removes two trees). The wooden door 0x06AD is not a row: TutorialFarm places a real door at DoorSpot.

namespace Server.Tutorial;

public static class TutorialFarmLayout
{
    /// <summary>The farmhouse's centre (the addon's anchor) and the lowest Z under it.</summary>
    public static readonly Point3D Anchor = new(3377, 2611, 15);

    /// <summary>The farmhouse door (east wall), dark wood, closed art 0x06AD.</summary>
    public static readonly Point3D DoorSpot = new(3380, 2611, 22);

    /// <summary>The two map statics P276's map patch removes (trunk and foliage of each tree): id, x, y, z.</summary>
    public static readonly (int Id, int X, int Y, int Z)[] RemovedTrees =
    [
        (0x0CE7, 3388, 2607, 19), // willow leaves
        (0x0CE6, 3388, 2607, 19), // willow tree
        (0x0CE1, 3396, 2616, 38), // walnut leaves
        (0x0CE0, 3396, 2616, 38), // walnut tree
    ];

    /// <summary>Every piece: art id, x, y, z (330 rows).</summary>
    public static readonly (int Id, int X, int Y, int Z)[] Pieces =
    [
        (0x0836, 3382, 2603, 9), // wooden fence
        (0x0836, 3382, 2619, 17), // wooden fence
        (0x0836, 3383, 2603, 12), // wooden fence
        (0x0836, 3383, 2619, 18), // wooden fence
        (0x0836, 3384, 2603, 12), // wooden fence
        (0x0836, 3384, 2619, 20), // wooden fence
        (0x0836, 3385, 2603, 13), // wooden fence
        (0x0836, 3385, 2619, 22), // wooden fence
        (0x0836, 3386, 2603, 15), // wooden fence
        (0x0836, 3386, 2619, 21), // wooden fence
        (0x0836, 3387, 2603, 15), // wooden fence
        (0x0836, 3387, 2619, 21), // wooden fence
        (0x0836, 3388, 2603, 15), // wooden fence
        (0x0836, 3388, 2619, 22), // wooden fence
        (0x0836, 3389, 2603, 18), // wooden fence
        (0x0836, 3389, 2619, 26), // wooden fence
        (0x0836, 3390, 2603, 18), // wooden fence
        (0x0836, 3390, 2619, 27), // wooden fence
        (0x0836, 3391, 2603, 19), // wooden fence
        (0x0836, 3391, 2619, 28), // wooden fence
        (0x0836, 3392, 2603, 22), // wooden fence
        (0x0836, 3392, 2619, 33), // wooden fence
        (0x0836, 3393, 2603, 25), // wooden fence
        (0x0836, 3393, 2619, 34), // wooden fence
        (0x0836, 3394, 2603, 25), // wooden fence
        (0x0836, 3394, 2619, 37), // wooden fence
        (0x0836, 3395, 2603, 26), // wooden fence
        (0x0836, 3395, 2619, 38), // wooden fence
        (0x0836, 3396, 2603, 28), // wooden fence
        (0x0836, 3396, 2619, 39), // wooden fence
        (0x0836, 3397, 2603, 34), // wooden fence
        (0x0836, 3397, 2619, 40), // wooden fence
        (0x0836, 3398, 2603, 35), // wooden fence
        (0x0836, 3398, 2619, 42), // wooden fence
        (0x0836, 3399, 2603, 36), // wooden fence
        (0x0836, 3399, 2619, 43), // wooden fence
        (0x0837, 3381, 2604, 11), // wooden fence
        (0x0837, 3399, 2604, 39), // wooden fence
        (0x0837, 3381, 2605, 10), // wooden fence
        (0x0837, 3399, 2605, 39), // wooden fence
        (0x0837, 3381, 2606, 11), // wooden fence
        (0x0837, 3399, 2606, 39), // wooden fence
        (0x0837, 3381, 2607, 10), // wooden fence
        (0x0837, 3399, 2607, 38), // wooden fence
        (0x0837, 3381, 2608, 13), // wooden fence
        (0x0837, 3399, 2608, 37), // wooden fence
        (0x0837, 3399, 2609, 40), // wooden fence
        (0x0837, 3399, 2610, 41), // wooden fence
        (0x0837, 3399, 2611, 39), // wooden fence
        (0x0837, 3399, 2612, 41), // wooden fence
        (0x0837, 3399, 2613, 42), // wooden fence
        (0x0837, 3381, 2614, 13), // wooden fence
        (0x0837, 3381, 2615, 12), // wooden fence
        (0x0837, 3381, 2616, 14), // wooden fence
        (0x0837, 3399, 2616, 44), // wooden fence
        (0x0837, 3381, 2617, 14), // wooden fence
        (0x0837, 3399, 2617, 43), // wooden fence
        (0x0837, 3381, 2618, 15), // wooden fence
        (0x0837, 3399, 2618, 44), // wooden fence
        (0x0837, 3381, 2619, 15), // wooden fence
        (0x0837, 3399, 2619, 43), // wooden fence
        (0x0835, 3381, 2603, 9), // wooden fence
        (0x0836, 3393, 2603, 25), // wooden fence
        (0x0836, 3393, 2609, 28), // wooden fence
        (0x0836, 3394, 2603, 25), // wooden fence
        (0x0836, 3394, 2609, 29), // wooden fence
        (0x0836, 3395, 2603, 26), // wooden fence
        (0x0836, 3395, 2609, 31), // wooden fence
        (0x0836, 3396, 2603, 28), // wooden fence
        (0x0836, 3396, 2609, 33), // wooden fence
        (0x0836, 3397, 2603, 34), // wooden fence
        (0x0836, 3397, 2609, 34), // wooden fence
        (0x0836, 3398, 2603, 35), // wooden fence
        (0x0836, 3398, 2609, 37), // wooden fence
        (0x0836, 3399, 2603, 36), // wooden fence
        (0x0836, 3399, 2609, 40), // wooden fence
        (0x0837, 3392, 2604, 22), // wooden fence
        (0x0837, 3399, 2604, 39), // wooden fence
        (0x0837, 3392, 2605, 23), // wooden fence
        (0x0837, 3399, 2605, 39), // wooden fence
        (0x0837, 3399, 2606, 39), // wooden fence
        (0x0837, 3399, 2607, 38), // wooden fence
        (0x0837, 3392, 2608, 28), // wooden fence
        (0x0837, 3399, 2608, 37), // wooden fence
        (0x0837, 3392, 2609, 28), // wooden fence
        (0x0837, 3399, 2609, 40), // wooden fence
        (0x0835, 3392, 2603, 22), // wooden fence
        (0x0B41, 3394, 2605, 26), // water trough
        (0x0B42, 3395, 2605, 30), // water trough
        (0x0F36, 3397, 2607, 33), // sheaf of hay
        (0x0F36, 3398, 2607, 36), // sheaf of hay
        (0x100C, 3397, 2608, 35), // hay sheave
        (0x0C7B, 3383, 2611, 13), // heads of cabbage
        (0x0C7B, 3384, 2611, 15), // heads of cabbage
        (0x0C7B, 3385, 2611, 16), // heads of cabbage
        (0x0C7B, 3386, 2611, 17), // heads of cabbage
        (0x0C7B, 3387, 2611, 21), // heads of cabbage
        (0x0C7B, 3388, 2611, 22), // heads of cabbage
        (0x0C7B, 3389, 2611, 23), // heads of cabbage
        (0x0C7B, 3390, 2611, 26), // heads of cabbage
        (0x0C7B, 3391, 2611, 27), // heads of cabbage
        (0x0C7B, 3392, 2611, 29), // heads of cabbage
        (0x0C7B, 3393, 2611, 30), // heads of cabbage
        (0x0C7B, 3394, 2611, 31), // heads of cabbage
        (0x0C7B, 3395, 2611, 34), // heads of cabbage
        (0x0C7B, 3396, 2611, 35), // heads of cabbage
        (0x0C7B, 3397, 2611, 37), // heads of cabbage
        (0x0C55, 3383, 2613, 15), // wheat
        (0x0C55, 3384, 2613, 17), // wheat
        (0x0C55, 3385, 2613, 17), // wheat
        (0x0C55, 3386, 2613, 19), // wheat
        (0x0C55, 3387, 2613, 27), // wheat
        (0x0C55, 3388, 2613, 22), // wheat
        (0x0C55, 3389, 2613, 24), // wheat
        (0x0C55, 3390, 2613, 26), // wheat
        (0x0C55, 3391, 2613, 27), // wheat
        (0x0C55, 3392, 2613, 29), // wheat
        (0x0C55, 3393, 2613, 30), // wheat
        (0x0C55, 3394, 2613, 34), // wheat
        (0x0C55, 3395, 2613, 34), // wheat
        (0x0C55, 3396, 2613, 35), // wheat
        (0x0C55, 3397, 2613, 39), // wheat
        (0x0C7D, 3383, 2615, 17), // corn stalk
        (0x0C7D, 3384, 2615, 16), // corn stalk
        (0x0C7D, 3385, 2615, 16), // corn stalk
        (0x0C7D, 3386, 2615, 19), // corn stalk
        (0x0C7D, 3387, 2615, 21), // corn stalk
        (0x0C7D, 3388, 2615, 23), // corn stalk
        (0x0C7D, 3389, 2615, 23), // corn stalk
        (0x0C7D, 3390, 2615, 27), // corn stalk
        (0x0C7D, 3391, 2615, 28), // corn stalk
        (0x0C7D, 3392, 2615, 30), // corn stalk
        (0x0C7D, 3393, 2615, 32), // corn stalk
        (0x0C7D, 3394, 2615, 33), // corn stalk
        (0x0C7D, 3395, 2615, 35), // corn stalk
        (0x0C7D, 3396, 2615, 38), // corn stalk
        (0x0C7D, 3397, 2615, 40), // corn stalk
        (0x0C76, 3383, 2617, 16), // carrots
        (0x0C76, 3384, 2617, 17), // carrots
        (0x0C76, 3385, 2617, 17), // carrots
        (0x0C76, 3386, 2617, 20), // carrots
        (0x0C76, 3387, 2617, 21), // carrots
        (0x0C76, 3388, 2617, 22), // carrots
        (0x0C76, 3389, 2617, 25), // carrots
        (0x0C76, 3390, 2617, 26), // carrots
        (0x0C76, 3391, 2617, 30), // carrots
        (0x0C76, 3392, 2617, 32), // carrots
        (0x0C76, 3393, 2617, 34), // carrots
        (0x0C76, 3394, 2617, 36), // carrots
        (0x0C76, 3395, 2617, 36), // carrots
        (0x0C76, 3396, 2617, 38), // carrots
        (0x0C76, 3397, 2617, 40), // carrots
        (0x0C6A, 3383, 2605, 13), // pumpkins
        (0x0C6A, 3384, 2605, 14), // pumpkins
        (0x0C6A, 3385, 2605, 15), // pumpkins
        (0x0C6A, 3386, 2605, 15), // pumpkins
        (0x0C6A, 3387, 2605, 16), // pumpkins
        (0x0C6A, 3388, 2605, 18), // pumpkins
        (0x0C6A, 3389, 2605, 18), // pumpkins
        (0x0C6A, 3390, 2605, 19), // pumpkins
        (0x0C7B, 3383, 2607, 14), // heads of cabbage
        (0x0C7B, 3384, 2607, 14), // heads of cabbage
        (0x0C7B, 3385, 2607, 16), // heads of cabbage
        (0x0C7B, 3386, 2607, 16), // heads of cabbage
        (0x0C7B, 3387, 2607, 16), // heads of cabbage
        (0x0C7B, 3388, 2607, 19), // heads of cabbage
        (0x0C7B, 3389, 2607, 21), // heads of cabbage
        (0x0C7B, 3390, 2607, 21), // heads of cabbage
        (0x1E34, 3388, 2612, 22), // scarecrow
        (0x0B97, 3401, 2613, 44), // wooden signpost
        (0x0BCF, 3401, 2613, 44), // wooden sign
        (0x0E77, 3400, 2616, 45), // barrel
        (0x04AC, 3377, 2611, 22), // wooden boards
        (0x05A6, 3377, 2611, 48), // thatch roof
        (0x04A9, 3378, 2611, 22), // wooden boards
        (0x04AB, 3376, 2610, 22), // wooden boards
        (0x04AB, 3377, 2612, 22), // wooden boards
        (0x04AA, 3378, 2610, 22), // wooden boards
        (0x04A9, 3378, 2612, 22), // wooden boards
        (0x04A9, 3377, 2610, 22), // wooden boards
        (0x04AA, 3376, 2611, 22), // wooden boards
        (0x04AC, 3376, 2612, 22), // wooden boards
        (0x05A6, 3376, 2610, 45), // thatch roof
        (0x05A6, 3376, 2611, 45), // thatch roof
        (0x05A6, 3376, 2612, 45), // thatch roof
        (0x05A6, 3377, 2610, 48), // thatch roof
        (0x05A6, 3377, 2612, 48), // thatch roof
        (0x05A5, 3378, 2612, 51), // thatch roof
        (0x05A5, 3378, 2611, 51), // thatch roof
        (0x05A5, 3378, 2610, 51), // thatch roof
        (0x04AB, 3378, 2613, 22), // wooden boards
        (0x04B0, 3376, 2609, 22), // wooden boards
        (0x04B0, 3378, 2609, 22), // wooden boards
        (0x04B0, 3377, 2609, 22), // wooden boards
        (0x04AA, 3377, 2613, 22), // wooden boards
        (0x04A9, 3376, 2613, 22), // wooden boards
        (0x04AD, 3375, 2610, 22), // wooden boards
        (0x04AD, 3375, 2611, 22), // wooden boards
        (0x04AD, 3375, 2612, 22), // wooden boards
        (0x04AA, 3379, 2612, 22), // wooden boards
        (0x04AB, 3379, 2611, 22), // wooden boards
        (0x04AC, 3379, 2610, 22), // wooden boards
        (0x05A6, 3375, 2612, 42), // thatch roof
        (0x05A6, 3375, 2611, 42), // thatch roof
        (0x05A6, 3375, 2610, 42), // thatch roof
        (0x05A6, 3376, 2609, 45), // thatch roof
        (0x05A6, 3376, 2613, 45), // thatch roof
        (0x05A6, 3377, 2609, 48), // thatch roof
        (0x05A6, 3377, 2613, 48), // thatch roof
        (0x05A4, 3379, 2610, 48), // thatch roof
        (0x05A4, 3379, 2611, 48), // thatch roof
        (0x05A4, 3379, 2612, 48), // thatch roof
        (0x05A5, 3378, 2609, 51), // thatch roof
        (0x05A5, 3378, 2613, 51), // thatch roof
        (0x0063, 3376, 2614, 15), // stone wall
        (0x0064, 3380, 2612, 15), // stone wall
        (0x0063, 3377, 2608, 15), // stone wall
        (0x0063, 3378, 2608, 15), // stone wall
        (0x0064, 3374, 2612, 15), // stone wall
        (0x0063, 3378, 2614, 15), // stone wall
        (0x0063, 3376, 2608, 15), // stone wall
        (0x0064, 3380, 2611, 15), // stone wall
        (0x0064, 3374, 2611, 15), // stone wall
        (0x0064, 3374, 2610, 15), // stone wall
        (0x0064, 3380, 2610, 15), // stone wall
        (0x00A7, 3374, 2611, 22), // wooden wall
        (0x04AF, 3380, 2610, 22), // wooden boards
        (0x00A8, 3376, 2608, 22), // wooden wall
        (0x00A8, 3377, 2608, 22), // wooden wall
        (0x00A8, 3378, 2608, 22), // wooden wall
        (0x00B9, 3374, 2610, 22), // window
        (0x00A7, 3374, 2612, 22), // wooden wall
        (0x04AF, 3380, 2611, 22), // wooden boards
        (0x04AF, 3380, 2612, 22), // wooden boards
        (0x04B0, 3379, 2609, 22), // wooden boards
        (0x04AE, 3376, 2614, 22), // wooden boards
        (0x04B4, 3375, 2609, 22), // wooden boards
        (0x04AE, 3377, 2614, 22), // wooden boards
        (0x04AC, 3379, 2613, 22), // wooden boards
        (0x04AE, 3378, 2614, 22), // wooden boards
        (0x04AD, 3375, 2613, 22), // wooden boards
        (0x00C1, 3376, 2614, 42), // wooden wall
        (0x00C1, 3377, 2614, 42), // wooden wall
        (0x05A6, 3375, 2609, 42), // thatch roof
        (0x00C1, 3378, 2614, 42), // wooden wall
        (0x05A6, 3375, 2613, 42), // thatch roof
        (0x00C1, 3378, 2614, 45), // wooden wall
        (0x00C1, 3377, 2614, 45), // wooden wall
        (0x05A4, 3380, 2612, 45), // thatch roof
        (0x05A4, 3380, 2611, 45), // thatch roof
        (0x05A4, 3380, 2610, 45), // thatch roof
        (0x05A6, 3376, 2614, 45), // thatch roof
        (0x05A6, 3377, 2614, 48), // thatch roof
        (0x00C1, 3378, 2614, 48), // wooden wall
        (0x05A4, 3379, 2609, 48), // thatch roof
        (0x05A4, 3379, 2613, 48), // thatch roof
        (0x05A5, 3378, 2614, 51), // thatch roof
        (0x0064, 3380, 2613, 15), // stone wall
        (0x0065, 3380, 2614, 15), // stone wall
        (0x0063, 3375, 2608, 15), // stone wall
        (0x0063, 3379, 2614, 15), // stone wall
        (0x0063, 3375, 2614, 15), // stone wall
        (0x0064, 3374, 2614, 15), // stone wall
        (0x0064, 3374, 2613, 15), // stone wall
        (0x0064, 3374, 2609, 15), // stone wall
        (0x0064, 3374, 2608, 15), // stone wall
        (0x0063, 3379, 2608, 15), // stone wall
        (0x0063, 3380, 2608, 15), // stone wall
        (0x0064, 3380, 2609, 15), // stone wall
        (0x04B1, 3380, 2614, 22), // wooden boards
        (0x04AF, 3380, 2613, 22), // wooden boards
        (0x04B3, 3380, 2609, 22), // wooden boards
        (0x04AE, 3379, 2614, 22), // wooden boards
        (0x04B2, 3375, 2614, 22), // wooden boards
        (0x00A6, 3380, 2614, 22), // wooden wall
        (0x00A7, 3380, 2609, 22), // wooden wall
        (0x00A8, 3380, 2608, 22), // wooden wall
        (0x00A8, 3379, 2608, 22), // wooden wall
        (0x00A8, 3375, 2608, 22), // wooden wall
        (0x00A7, 3374, 2609, 22), // wooden wall
        (0x00A7, 3374, 2614, 22), // wooden wall
        (0x00B9, 3380, 2613, 22), // window
        (0x00B9, 3374, 2613, 22), // window
        (0x00A8, 3375, 2614, 22), // wooden wall
        (0x00A8, 3379, 2614, 22), // wooden wall
        (0x00A9, 3374, 2608, 22), // wooden post
        (0x05A4, 3381, 2613, 42), // thatch roof
        (0x05A4, 3381, 2612, 42), // thatch roof
        (0x05A4, 3381, 2611, 42), // thatch roof
        (0x05A4, 3381, 2610, 42), // thatch roof
        (0x05A6, 3375, 2615, 42), // thatch roof
        (0x05A6, 3375, 2614, 42), // thatch roof
        (0x00C1, 3379, 2614, 42), // wooden wall
        (0x00C1, 3380, 2614, 42), // wooden wall
        (0x05A4, 3381, 2609, 42), // thatch roof
        (0x05A4, 3380, 2614, 45), // thatch roof
        (0x00C1, 3379, 2614, 45), // wooden wall
        (0x05A6, 3376, 2615, 45), // thatch roof
        (0x05A4, 3380, 2609, 45), // thatch roof
        (0x05A4, 3380, 2613, 45), // thatch roof
        (0x05A6, 3377, 2615, 48), // thatch roof
        (0x05A4, 3379, 2614, 48), // thatch roof
        (0x05A4, 3379, 2615, 48), // thatch roof
        (0x05A5, 3378, 2615, 51), // thatch roof
        (0x05A4, 3381, 2614, 42), // thatch roof
        (0x05A4, 3380, 2615, 45), // thatch roof
        (0x05A4, 3381, 2615, 42), // thatch roof
        (0x00A8, 3376, 2614, 22), // wooden wall
        (0x00BA, 3377, 2614, 22), // window
        (0x00A8, 3378, 2614, 22), // wooden wall
        (0x00AB, 3380, 2610, 22), // wooden wall
        (0x00AD, 3380, 2612, 22), // wooden wall
        (0x04AF, 3381, 2609, 20), // wooden boards
        (0x04AF, 3381, 2610, 20), // wooden boards
        (0x04A9, 3381, 2611, 20), // wooden boards
        (0x04AF, 3381, 2612, 20), // wooden boards
        (0x04AF, 3381, 2613, 20), // wooden boards
        (0x0B2C, 3381, 2609, 22), // wooden bench
        (0x154D, 3381, 2613, 22), // water barrel
        (0x0725, 3382, 2611, 17), // wooden stairs
        (0x0725, 3382, 2610, 17), // wooden stairs
        (0x0725, 3382, 2612, 17), // wooden stairs
        (0x08D5, 3373, 2611, 15), // chimney
        (0x0F36, 3375, 2607, 15), // sheaf of hay
        (0x100C, 3376, 2607, 15), // hay sheave
        (0x0F36, 3377, 2607, 15), // sheaf of hay
        (0x0E87, 3378, 2607, 15), // pitchfork
        (0x1E34, 3379, 2606, 15), // scarecrow
        (0xA300, 3372, 2615, 15), // wishing_well_east
        (0x4514, 3375, 2605, 15), // ChickenCoop South
    ];
}
