// ServUO: Services/Underworld/Maze of Death/UnderworldPuzzle.cs (cc-P197). The three types ServUO keeps in this
// file: the Maze of Death's puzzle board (UnderworldPuzzleItem, 1113379, hue 914, a blessed BaseDecayingItem of
// 1800 seconds the UnderworldPuzzleBox hands out once a day), its solution (one of sixteen target rows of coloured
// pieces, each with its own attempt limit of 4 to 8, and the matching start row), and the gump that plays it: four
// rows, a row selector, Up and Down moves that split, swap or combine pieces (red + blue = green and so on,
// SplitPiecesUp / SplitPiecesDown / CombinePieces, ServUO's arithmetic over the PuzzlePiece enum), the move
// counter (1150180), the Crystals Locked line for a player with 100 Lockpicking (1150179), Okay to submit and Cancel
// to reset. A matching row (all four pieces) pays one of the twelve rewards (30 of a venom, essence or sac, 2-6
// lucky coins, or a decoration), 1113579, and deletes the board. The two sixteen-case tables are spliced from
// ServUO's source by gen_maze.py (notes), not retyped. The board does not survive a restart: ServUO's Deserialize
// deletes it (here AfterDeserialization), so nothing of it is saved. ServUO's commented-out attempt reset is left
// out; its MaxAttempts static is kept though nothing reads it.

using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class UnderworldPuzzleItem : BaseDecayingItem
{
    public static readonly int MaxAttempts = 8;

    private UnderworldPuzzleSolution _solution;
    private UnderworldPuzzleSolution _currentSolution;
    private int _attempts;

    public UnderworldPuzzleSolution Solution => _solution;

    public UnderworldPuzzleSolution CurrentSolution
    {
        get => _currentSolution;
        set => _currentSolution = value;
    }

    public override int LabelNumber => 1113379; // Puzzle Board

    [CommandProperty(AccessLevel.GameMaster)]
    public int Attempts
    {
        get => _attempts;
        set => _attempts = value;
    }

    public override int Lifespan => 1800;

    [Constructible]
    public UnderworldPuzzleItem() : base(0x2AAA)
    {
        Hue = 914;
        _attempts = 0;

        _solution = new UnderworldPuzzleSolution();
        _currentSolution = new UnderworldPuzzleSolution(_solution.Index);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
        else
        {
            from.CloseGump<UnderworldPuzzleGump>();
            from.SendGump(new UnderworldPuzzleGump(from, this));
        }
    }

    public bool SubmitSolution(Mobile m, UnderworldPuzzleSolution solution)
    {
        if (solution.Matches(_solution))
        {
            var item = Loot.Construct(Rewards[Utility.Random(Rewards.Length)]);

            if (item != null)
            {
                if (item is VoidEssence or SilverSerpentVenom or ToxicVenomSac)
                {
                    item.Amount = 30;
                }

                if (item is LuckyCoin)
                {
                    item.Amount = Utility.RandomMinMax(2, 6);
                }

                if (m.Backpack == null || !m.Backpack.TryDropItem(m, item, false))
                {
                    m.BankBox.DropItem(item);
                }
            }

            m.PlaySound(0x3D);
            m.SendLocalizedMessage(1113579); // Correct Code Entered. Crystal Lock Disengaged.

            Delete();
            return true;
        }

        return false;
    }

    public static readonly Type[] Rewards =
    {
        typeof(VoidEssence), typeof(SilverSerpentVenom), typeof(ScouringToxin),
        typeof(ToxicVenomSac), typeof(MouldingBoard), typeof(DoughBowl),
        typeof(HornedTotemPole), typeof(LargeSquarePillow), typeof(LargeDiamondPillow),
        typeof(DustyPillow), typeof(StatuePedestal), /* typeof(FlouredBreadBoard), */
        typeof(LuckyCoin)
    };

    public override void OnDelete()
    {
        base.OnDelete();

        if (RootParent is Mobile m)
        {
            m.CloseGump<UnderworldPuzzleGump>();
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Delete();
    }
}

public enum PuzzlePiece
{
    None,
    RedSingle,
    RedDouble,
    RedTriple,
    RedBar,
    BlueSingle,
    BlueDouble,
    BlueTriple,
    BlueBar,
    GreenSingle,
    GreenDouble,
    GreenTriple,
    GreenBar
}

public enum PuzzleColor
{
    Red,
    Blue,
    Green
}

public class UnderworldPuzzleSolution
{
    public const int Length = 4;

    private readonly PuzzlePiece[] _rows = new PuzzlePiece[Length];
    private int _index;
    private int _maxAttempts;

    public PuzzlePiece[] Rows => _rows;
    public int Index => _index;
    public int MaxAttempts => _maxAttempts;

    public PuzzlePiece First
    {
        get => _rows[0];
        set => _rows[0] = value;
    }

    public PuzzlePiece Second
    {
        get => _rows[1];
        set => _rows[1] = value;
    }

    public PuzzlePiece Third
    {
        get => _rows[2];
        set => _rows[2] = value;
    }

    public PuzzlePiece Fourth
    {
        get => _rows[3];
        set => _rows[3] = value;
    }

    public UnderworldPuzzleSolution()
    {
        PickRandom();
    }

    public UnderworldPuzzleSolution(int index)
    {
        LoadStartSolution(index);
    }

    public UnderworldPuzzleSolution(PuzzlePiece first, PuzzlePiece second, PuzzlePiece third, PuzzlePiece fourth)
    {
        First = first;
        Second = second;
        Third = third;
        Fourth = fourth;
    }

    public bool Matches(UnderworldPuzzleSolution check) => GetMatches(check) >= 4;

    public int GetMatches(UnderworldPuzzleSolution check)
    {
        var matches = 0;

        for (var i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] == check.Rows[i])
            {
                matches++;
            }
        }

        return matches;
    }

    public void PickRandom()
    {
        _index = Utility.Random(16);
        switch (_index)
        {
            case 0:
                First = PuzzlePiece.RedSingle;
                Second = PuzzlePiece.BlueSingle;
                Third = PuzzlePiece.RedSingle;
                Fourth = PuzzlePiece.GreenSingle;
                _maxAttempts = 6;
                break;
            case 1:
                First = PuzzlePiece.GreenDouble;
                Second = PuzzlePiece.RedBar;
                Third = PuzzlePiece.None;
                Fourth = PuzzlePiece.BlueTriple;
                _maxAttempts = 6;
                break;
            case 2:
                First = PuzzlePiece.None;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.RedBar;
                Fourth = PuzzlePiece.RedTriple;
                _maxAttempts = 4;
                break;
            case 3:
                First = PuzzlePiece.BlueDouble;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.GreenDouble;
                Fourth = PuzzlePiece.GreenDouble;
                _maxAttempts = 7;
                break;
            case 4:
                First = PuzzlePiece.BlueSingle;
                Second = PuzzlePiece.GreenSingle;
                Third = PuzzlePiece.GreenDouble;
                Fourth = PuzzlePiece.RedBar;
                _maxAttempts = 7;
                break;
            case 5:
                First = PuzzlePiece.GreenDouble;
                Second = PuzzlePiece.BlueBar;
                Third = PuzzlePiece.RedSingle;
                Fourth = PuzzlePiece.BlueSingle;
                _maxAttempts = 8;
                break;
            case 6:
                First = PuzzlePiece.GreenSingle;
                Second = PuzzlePiece.RedSingle;
                Third = PuzzlePiece.BlueDouble;
                Fourth = PuzzlePiece.GreenBar;
                _maxAttempts = 5;
                break;
            case 7:
                First = PuzzlePiece.BlueDouble;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.BlueTriple;
                Fourth = PuzzlePiece.None;
                _maxAttempts = 4;
                break;
            case 8:
                First = PuzzlePiece.GreenSingle;
                Second = PuzzlePiece.GreenBar;
                Third = PuzzlePiece.RedDouble;
                Fourth = PuzzlePiece.RedSingle;
                _maxAttempts = 7;
                break;
            case 9:
                First = PuzzlePiece.BlueSingle;
                Second = PuzzlePiece.GreenDouble;
                Third = PuzzlePiece.None;
                Fourth = PuzzlePiece.GreenTriple;
                _maxAttempts = 6;
                break;
            case 10:
                First = PuzzlePiece.BlueSingle;
                Second = PuzzlePiece.RedSingle;
                Third = PuzzlePiece.RedTriple;
                Fourth = PuzzlePiece.None;
                _maxAttempts = 6;
                break;
            case 11:
                First = PuzzlePiece.GreenSingle;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.GreenTriple;
                Fourth = PuzzlePiece.GreenDouble;
                _maxAttempts = 5;
                break;
            case 12:
                First = PuzzlePiece.RedTriple;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.None;
                Fourth = PuzzlePiece.RedTriple;
                _maxAttempts = 6;
                break;
            case 13:
                First = PuzzlePiece.None;
                Second = PuzzlePiece.BlueTriple;
                Third = PuzzlePiece.GreenSingle;
                Fourth = PuzzlePiece.BlueDouble;
                _maxAttempts = 7;
                break;
            case 14:
                First = PuzzlePiece.BlueTriple;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.BlueTriple;
                Fourth = PuzzlePiece.None;
                _maxAttempts = 6;
                break;
            case 15:
                First = PuzzlePiece.RedSingle;
                Second = PuzzlePiece.BlueDouble;
                Third = PuzzlePiece.RedDouble;
                Fourth = PuzzlePiece.GreenSingle;
                _maxAttempts = 6;
                break;
        }
    }

    public void LoadStartSolution(int index)
    {
        _index = index;

        switch (index)
        {
            case 0:
                First = PuzzlePiece.RedSingle;
                Second = PuzzlePiece.RedSingle;
                Third = PuzzlePiece.RedSingle;
                Fourth = PuzzlePiece.RedSingle;
                break;
            case 1:
                First = PuzzlePiece.BlueBar;
                Second = PuzzlePiece.RedDouble;
                Third = PuzzlePiece.RedBar;
                Fourth = PuzzlePiece.BlueSingle;
                break;
            case 2:
                First = PuzzlePiece.GreenBar;
                Second = PuzzlePiece.BlueDouble;
                Third = PuzzlePiece.RedTriple;
                Fourth = PuzzlePiece.RedBar;
                break;
            case 3:
                First = PuzzlePiece.RedSingle;
                Second = PuzzlePiece.GreenDouble;
                Third = PuzzlePiece.BlueTriple;
                Fourth = PuzzlePiece.RedBar;
                break;
            case 4:
                First = PuzzlePiece.GreenSingle;
                Second = PuzzlePiece.RedBar;
                Third = PuzzlePiece.RedDouble;
                Fourth = PuzzlePiece.GreenSingle;
                break;
            case 5:
                First = PuzzlePiece.RedBar;
                Second = PuzzlePiece.GreenBar;
                Third = PuzzlePiece.RedBar;
                Fourth = PuzzlePiece.BlueBar;
                break;
            case 6:
                First = PuzzlePiece.RedBar;
                Second = PuzzlePiece.GreenDouble;
                Third = PuzzlePiece.GreenDouble;
                Fourth = PuzzlePiece.RedBar;
                break;
            case 7:
                First = PuzzlePiece.GreenBar;
                Second = PuzzlePiece.BlueSingle;
                Third = PuzzlePiece.BlueTriple;
                Fourth = PuzzlePiece.RedSingle;
                break;
            case 8:
                First = PuzzlePiece.GreenBar;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.GreenTriple;
                Fourth = PuzzlePiece.GreenSingle;
                break;
            case 9:
                First = PuzzlePiece.RedBar;
                Second = PuzzlePiece.RedBar;
                Third = PuzzlePiece.RedBar;
                Fourth = PuzzlePiece.RedBar;
                break;
            case 10:
                First = PuzzlePiece.RedSingle;
                Second = PuzzlePiece.None;
                Third = PuzzlePiece.BlueDouble;
                Fourth = PuzzlePiece.RedBar;
                break;
            case 11:
                First = PuzzlePiece.RedDouble;
                Second = PuzzlePiece.GreenDouble;
                Third = PuzzlePiece.RedDouble;
                Fourth = PuzzlePiece.BlueDouble;
                break;
            case 12:
                First = PuzzlePiece.GreenTriple;
                Second = PuzzlePiece.BlueBar;
                Third = PuzzlePiece.GreenTriple;
                Fourth = PuzzlePiece.RedBar;
                break;
            case 13:
                First = PuzzlePiece.RedSingle;
                Second = PuzzlePiece.GreenBar;
                Third = PuzzlePiece.GreenBar;
                Fourth = PuzzlePiece.RedSingle;
                break;
            case 14:
                First = PuzzlePiece.GreenTriple;
                Second = PuzzlePiece.GreenBar;
                Third = PuzzlePiece.BlueBar;
                Fourth = PuzzlePiece.GreenSingle;
                break;
            case 15:
                First = PuzzlePiece.BlueTriple;
                Second = PuzzlePiece.GreenDouble;
                Third = PuzzlePiece.BlueSingle;
                Fourth = PuzzlePiece.RedDouble;
                break;
        }
    }
}

public class UnderworldPuzzleGump : Gump
{
    private readonly Mobile _from;
    private readonly UnderworldPuzzleItem _item;
    private readonly UnderworldPuzzleSolution _solution;
    private readonly UnderworldPuzzleSolution _currentSolution;
    private int _row;

    public UnderworldPuzzleGump(Mobile from, UnderworldPuzzleItem item) : this(from, item, 0)
    {
    }

    public UnderworldPuzzleGump(Mobile from, UnderworldPuzzleItem item, int row) : base(45, 45)
    {
        if (row > 3)
        {
            row = 3;
        }

        if (row < 0)
        {
            row = 0;
        }

        _from = from;
        _item = item;
        _row = row;

        _solution = item.Solution;
        _currentSolution = item.CurrentSolution;

        AddBackground(50, 50, 500, 200, 9250);

        AddImageTiled(85, 210, 17, 150, 9255);
        AddImageTiled(110, 63, 17, 150, 9255);

        AddImageTiled(140, 90, 250, 17, 9251);
        AddImageTiled(60, 125, 350, 17, 9251);
        AddImageTiled(140, 160, 250, 17, 9251);
        AddImageTiled(60, 195, 350, 17, 9251);

        AddBackground(70, 70, 90, 155, 9250);
        AddBackground(200, 70, 140, 155, 9250);
        AddBackground(390, 70, 140, 155, 9250);

        AddBackground(50, 280, 200, 120, 9250);

        AddImage(0, 0, 10400);
        AddImage(0, 170, 10401);
        AddImage(0, 350, 10402);

        AddButton(105, 87, row == 0 ? 208 : 209, row == 0 ? 209 : 208, 1);
        AddButton(105, 122, row == 1 ? 208 : 209, row == 0 ? 209 : 208, 2);
        AddButton(105, 157, row == 2 ? 208 : 209, row == 0 ? 209 : 208, 3);
        AddButton(105, 192, row == 3 ? 208 : 209, row == 0 ? 209 : 208, 4);

        AddPiece(0, true, _solution.First);
        AddPiece(1, true, _solution.Second);
        AddPiece(2, true, _solution.Third);
        AddPiece(3, true, _solution.Fourth);

        AddPiece(0, false, _currentSolution.First);
        AddPiece(1, false, _currentSolution.Second);
        AddPiece(2, false, _currentSolution.Third);
        AddPiece(3, false, _currentSolution.Fourth);

        for (var i = 0; i < 4; i++)
        {
            if (i == row && _currentSolution.Rows[i] != PuzzlePiece.None && _item.Attempts < _solution.MaxAttempts)
            {
                AddButton(85, 87 + i * 35, 2650, 2650, 5);  // Up
                AddButton(125, 87 + i * 35, 2648, 2648, 6); // Down
            }
            else
            {
                AddImage(85, 87 + i * 35, 2709);
                AddImage(125, 87 + i * 35, 2709);
            }
        }

        AddButton(85, 87 + row * 35, 2650, 2650, 5);  // Up
        AddButton(125, 87 + row * 35, 2648, 2648, 6); // Down

        AddHtmlLocalized(65, 295, 130, 16, 1150180); // Command Functions:
        AddLabel(200, 295, 0, $"{_item.Attempts}/{_solution.MaxAttempts}");

        if (from.Skills[SkillName.Lockpicking].Base >= 100.0)
        {
            var locked = _solution.GetMatches(_currentSolution);
            AddHtmlLocalized(65, 310, 120, 16, 1150179); // Crystals Locked  :
            AddLabel(190, 310, 0, locked.ToString());
        }

        AddButton(80, 335, 2124, 2123, 7);  // Okay
        AddButton(160, 335, 2073, 2072, 8); // Cancel
        AddButton(120, 360, 2011, 2010, 0); // Logout
    }

    private void AddPiece(int row, bool right, PuzzlePiece piece)
    {
        var id = GetPuzzlePieceID(piece);
        var x = right ? 405 : 215;
        var y = 82 + 35 * row;

        switch (piece)
        {
            case PuzzlePiece.None:
                break;
            case PuzzlePiece.RedSingle:
            case PuzzlePiece.BlueSingle:
            case PuzzlePiece.GreenSingle:
                AddImage(x + 40, y, id);
                break;
            case PuzzlePiece.RedDouble:
            case PuzzlePiece.BlueDouble:
            case PuzzlePiece.GreenDouble:
                AddImage(x, y, id);
                AddImage(x + 80, y, id);
                break;
            case PuzzlePiece.RedTriple:
            case PuzzlePiece.BlueTriple:
            case PuzzlePiece.GreenTriple:
                AddImage(x, y, id);
                AddImage(x + 40, y, id);
                AddImage(x + 80, y, id);
                break;
            case PuzzlePiece.RedBar:
            case PuzzlePiece.BlueBar:
            case PuzzlePiece.GreenBar:
                AddImage(x, y, id);
                break;
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_item.Deleted || info.ButtonID == 0 || !_from.CheckAlive())
        {
            return;
        }

        if (_from.AccessLevel == AccessLevel.Player && !_item.IsChildOf(_from.Backpack))
        {
            _from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 500446); // That is too far away.
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                break;
            default:
                {
                    _row = info.ButtonID - 1;
                    break;
                }
            case 5:
                {
                    var nextRow = _row - 1;
                    if (nextRow < 0)
                    {
                        nextRow = 3;
                    }

                    var movingPiece = _currentSolution.Rows[_row];
                    var movingToPiece = _currentSolution.Rows[nextRow];

                    // Can't move empty spaces
                    if (movingPiece == PuzzlePiece.None || _item.Attempts >= _solution.MaxAttempts)
                    {
                        break;
                    }

                    SplitPiecesUp(ref movingPiece, ref movingToPiece);

                    if (movingPiece != _currentSolution.Rows[_row] || movingToPiece != _currentSolution.Rows[nextRow])
                    {
                        _currentSolution.Rows[_row] = movingPiece;
                        _currentSolution.Rows[nextRow] = movingToPiece;

                        _item.CurrentSolution = _currentSolution;
                        _item.Attempts++;
                    }

                    break;
                }
            case 6:
                {
                    var nextRow = _row + 1;
                    if (nextRow > 3)
                    {
                        nextRow = 0;
                    }

                    var movingPiece = _currentSolution.Rows[_row];
                    var movingToPiece = _currentSolution.Rows[nextRow];

                    // Can't Move Empty Spaces
                    if (movingPiece == PuzzlePiece.None || _item.Attempts >= _solution.MaxAttempts)
                    {
                        break;
                    }

                    SplitPiecesDown(ref movingPiece, ref movingToPiece);

                    if (movingPiece != _currentSolution.Rows[_row] || movingToPiece != _currentSolution.Rows[nextRow])
                    {
                        _currentSolution.Rows[_row] = movingPiece;
                        _currentSolution.Rows[nextRow] = movingToPiece;

                        _item.Attempts++;

                        _item.CurrentSolution = _currentSolution;
                    }

                    break;
                }
            case 7:
                {
                    if (_item.SubmitSolution(_from, _currentSolution))
                    {
                        return;
                    }

                    break;
                }
            case 8:
                {
                    var index = _item.Solution.Index;
                    _item.CurrentSolution = new UnderworldPuzzleSolution(index);
                    _item.Attempts = 0;
                    break;
                }
        }

        _from.SendGump(new UnderworldPuzzleGump(_from, _item, _row));
    }

    public static int GetTotalPieces(PuzzlePiece piece)
    {
        switch (piece)
        {
            default:
                return 0;
            case PuzzlePiece.RedSingle:
            case PuzzlePiece.BlueSingle:
            case PuzzlePiece.GreenSingle:
                return 1;
            case PuzzlePiece.RedDouble:
            case PuzzlePiece.BlueDouble:
            case PuzzlePiece.GreenDouble:
                return 2;
            case PuzzlePiece.RedTriple:
            case PuzzlePiece.BlueTriple:
            case PuzzlePiece.GreenTriple:
                return 3;
            case PuzzlePiece.RedBar:
            case PuzzlePiece.BlueBar:
            case PuzzlePiece.GreenBar:
                return 4;
        }
    }

    public static void SplitPiecesUp(ref PuzzlePiece movingPiece, ref PuzzlePiece movingToPiece)
    {
        var movingAmount = GetTotalPieces(movingPiece);
        var moveToAmount = GetTotalPieces(movingToPiece);

        if (movingToPiece == PuzzlePiece.None)
        {
            if (movingAmount == 2 || movingAmount == 4)
            {
                switch (movingPiece)
                {
                    case PuzzlePiece.RedDouble:
                        movingToPiece = PuzzlePiece.BlueSingle;
                        movingPiece = PuzzlePiece.GreenSingle;
                        break;
                    case PuzzlePiece.BlueDouble:
                        movingToPiece = PuzzlePiece.GreenSingle;
                        movingPiece = PuzzlePiece.RedSingle;
                        break;
                    case PuzzlePiece.GreenDouble:
                        movingToPiece = PuzzlePiece.RedSingle;
                        movingPiece = PuzzlePiece.BlueSingle;
                        break;
                    case PuzzlePiece.RedBar:
                        movingToPiece = PuzzlePiece.BlueDouble;
                        movingPiece = PuzzlePiece.GreenDouble;
                        break;
                    case PuzzlePiece.BlueBar:
                        movingToPiece = PuzzlePiece.GreenDouble;
                        movingPiece = PuzzlePiece.RedDouble;
                        break;
                    case PuzzlePiece.GreenBar:
                        movingToPiece = PuzzlePiece.RedDouble;
                        movingPiece = PuzzlePiece.BlueDouble;
                        break;
                }
            }

            return;
        }

        if (movingAmount + moveToAmount > 4)
        {
            (movingPiece, movingToPiece) = (movingToPiece, movingPiece);

            return;
        }

        movingToPiece = CombinePieces(movingPiece, movingToPiece);
        movingPiece = PuzzlePiece.None;
    }

    public static void SplitPiecesDown(ref PuzzlePiece movingPiece, ref PuzzlePiece movingToPiece)
    {
        var movingAmount = GetTotalPieces(movingPiece);
        var moveToAmount = GetTotalPieces(movingToPiece);

        if (movingToPiece == PuzzlePiece.None)
        {
            if (movingAmount == 2 || movingAmount == 4)
            {
                switch (movingPiece)
                {
                    case PuzzlePiece.RedDouble:
                        movingToPiece = PuzzlePiece.BlueSingle;
                        movingPiece = PuzzlePiece.GreenSingle;
                        break;
                    case PuzzlePiece.BlueDouble:
                        movingToPiece = PuzzlePiece.GreenSingle;
                        movingPiece = PuzzlePiece.RedSingle;
                        break;
                    case PuzzlePiece.GreenDouble:
                        movingToPiece = PuzzlePiece.RedSingle;
                        movingPiece = PuzzlePiece.BlueSingle;
                        break;
                    case PuzzlePiece.RedBar:
                        movingToPiece = PuzzlePiece.BlueDouble;
                        movingPiece = PuzzlePiece.GreenDouble;
                        break;
                    case PuzzlePiece.BlueBar:
                        movingToPiece = PuzzlePiece.GreenDouble;
                        movingPiece = PuzzlePiece.RedDouble;
                        break;
                    case PuzzlePiece.GreenBar:
                        movingToPiece = PuzzlePiece.RedDouble;
                        movingPiece = PuzzlePiece.BlueDouble;
                        break;
                }
            }

            return;
        }

        var t = 0;

        if (moveToAmount > movingAmount)
        {
            t = (int)movingToPiece - movingAmount;
        }
        else if (movingAmount > moveToAmount)
        {
            if (movingAmount == 4 && moveToAmount == 3)
            {
                t = (int)movingToPiece - 2;
            }
            else if (movingAmount == 4 && moveToAmount == 2)
            {
                t = (int)movingToPiece;
            }
            else if (movingAmount == 4 && moveToAmount == 1)
            {
                t = (int)movingToPiece + 2;
            }
            else if (movingAmount == 2 && moveToAmount == 1)
            {
                t = (int)movingToPiece;
            }
            else if (movingAmount == 3 && moveToAmount == 2)
            {
                t = (int)movingToPiece - 1;
            }
            else if (movingAmount == 3 && moveToAmount == 1)
            {
                t = (int)movingToPiece + 1;
            }
        }
        else
        {
            movingPiece = PuzzlePiece.None;
            movingToPiece = PuzzlePiece.None;
            return;
        }

        movingToPiece = (PuzzlePiece)t;
        movingPiece = PuzzlePiece.None;
    }

    public static PuzzlePiece CombinePieces(PuzzlePiece moving, PuzzlePiece movingInto)
    {
        var movingAmount = GetTotalPieces(moving);
        var moveToAmount = GetTotalPieces(movingInto);
        var combined = movingAmount + moveToAmount;

        if (movingAmount != moveToAmount)
        {
            var t = (int)movingInto + movingAmount;
            return (PuzzlePiece)t;
        }

        combined--;

        var movingCol = GetColor(moving);
        var movingIntoCol = GetColor(movingInto);

        switch (movingCol)
        {
            case PuzzleColor.Red:
                switch (movingIntoCol)
                {
                    case PuzzleColor.Red:
                        return PuzzlePiece.RedSingle + combined;
                    case PuzzleColor.Blue:
                        return PuzzlePiece.GreenSingle + combined;
                    case PuzzleColor.Green:
                        return PuzzlePiece.BlueSingle + combined;
                }

                break;
            case PuzzleColor.Blue:
                switch (movingIntoCol)
                {
                    case PuzzleColor.Red:
                        return PuzzlePiece.GreenSingle + combined;
                    case PuzzleColor.Blue:
                        return PuzzlePiece.BlueSingle + combined;
                    case PuzzleColor.Green:
                        return PuzzlePiece.RedSingle + combined;
                }

                break;
            case PuzzleColor.Green:
                switch (movingIntoCol)
                {
                    case PuzzleColor.Red:
                        return PuzzlePiece.BlueSingle + combined;
                    case PuzzleColor.Blue:
                        return PuzzlePiece.RedSingle + combined;
                    case PuzzleColor.Green:
                        return PuzzlePiece.GreenSingle + combined;
                }

                break;
        }

        return moving;
    }

    public static PuzzleColor GetColor(PuzzlePiece piece)
    {
        switch (piece)
        {
            default:
            case PuzzlePiece.RedSingle:
            case PuzzlePiece.RedDouble:
            case PuzzlePiece.RedTriple:
            case PuzzlePiece.RedBar:
                return PuzzleColor.Red;
            case PuzzlePiece.BlueSingle:
            case PuzzlePiece.BlueDouble:
            case PuzzlePiece.BlueTriple:
            case PuzzlePiece.BlueBar:
                return PuzzleColor.Blue;
            case PuzzlePiece.GreenSingle:
            case PuzzlePiece.GreenDouble:
            case PuzzlePiece.GreenTriple:
            case PuzzlePiece.GreenBar:
                return PuzzleColor.Green;
        }
    }

    private static int GetPuzzlePieceID(PuzzlePiece piece)
    {
        switch (piece)
        {
            default:
            case PuzzlePiece.None:
                return 0x0;
            case PuzzlePiece.RedSingle:
            case PuzzlePiece.RedDouble:
            case PuzzlePiece.RedTriple:
                return 0x2A62;
            case PuzzlePiece.BlueSingle:
            case PuzzlePiece.BlueDouble:
            case PuzzlePiece.BlueTriple:
                return 0x2A3A;
            case PuzzlePiece.GreenSingle:
            case PuzzlePiece.GreenDouble:
            case PuzzlePiece.GreenTriple:
                return 0x2A4E;
            case PuzzlePiece.RedBar:
                return 0x2A58;
            case PuzzlePiece.BlueBar:
                return 0x2A30;
            case PuzzlePiece.GreenBar:
                return 0x2A44;
        }
    }
}
