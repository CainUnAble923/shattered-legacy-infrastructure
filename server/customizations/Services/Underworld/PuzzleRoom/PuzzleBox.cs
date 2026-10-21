// ServUO: Services/Underworld/PuzzleRoom/PuzzleBox.cs (cc-P197). The three puzzle boxes of the Puzzle Room
// (1090,1171 west, 1104,1171 east, 1097,1163 north, all z 11, Ter Mur): within three tiles, a player carrying a
// magic key gets a maze board from the west or east box and, with a gold puzzle key too, the mastermind board
// from the north box; one board at a time ("You already have a puzzle board."). ServUO's order is kept: a player
// with no magic key is first thrown out to 1095-1099,1178-1179 z -1 with their pets and smoke, and the box then
// goes on to say which key is missing. ServUO's messages are plain strings (its spelling "recieve" included).

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

public enum PuzzleType
{
    WestBox,  // Maze #1
    EastBox,  // Maze #2
    NorthBox  // Mastermind
}

[SerializationGenerator(0, false)]
public partial class PuzzleBox : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PuzzleType _puzzleType;

    [Constructible]
    public PuzzleBox(PuzzleType type) : base(2472)
    {
        _puzzleType = type;
        Movable = false;
    }

    public override int LabelNumber => 1113486; // a puzzle box

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(Location, 3))
        {
            from.SendLocalizedMessage(3000268); // that is too far away.
        }
        else if (from.Backpack != null)
        {
            Item puzzle = null;

            var key = from.Backpack.FindItemByType<MagicKey>();
            var puzzleItem1 = from.Backpack.FindItemByType<MazePuzzleItem>();
            var puzzleItem2 = from.Backpack.FindItemByType<MastermindPuzzleItem>();

            if (key == null)
            {
                var x = Utility.RandomMinMax(1095, 1099);
                var y = Utility.RandomMinMax(1178, 1179);
                var z = -1;

                var loc = from.Location;
                var p = new Point3D(x, y, z);
                BaseCreature.TeleportPets(from, p, Map.TerMur);
                from.MoveToWorld(p, Map.TerMur);

                from.PlaySound(0x1FE);
                Effects.SendLocationParticles(EffectItem.Create(loc, from.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
                Effects.SendLocationParticles(EffectItem.Create(p, from.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);
            }

            if (puzzleItem1 != null || puzzleItem2 != null)
            {
                from.SendMessage("You already have a puzzle board.");
                return;
            }

            Item item = _puzzleType switch
            {
                PuzzleType.NorthBox => from.Backpack.FindItemByType<GoldPuzzleKey>(),
                _                   => from.Backpack.FindItemByType<MagicKey>()
            };

            if (item != null && key != null)
            {
                if (_puzzleType == PuzzleType.NorthBox)
                {
                    puzzle = new MastermindPuzzleItem(key);
                }
                else
                {
                    puzzle = new MazePuzzleItem(key);
                }
            }

            if (puzzle != null)
            {
                if (!from.Backpack.TryDropItem(from, puzzle, true))
                {
                    puzzle.Delete();
                }
                else
                {
                    from.SendMessage("You recieve a puzzle board.");
                }
            }
            else
            {
                from.SendMessage("You do not have the required key to get that puzzle board.");
            }
        }
    }
}
