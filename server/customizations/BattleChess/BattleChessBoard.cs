// BattleChessBoard.cs
//
// cc-P862: the Battle Chess board, a 10 x 10 addon: the 8 x 8 marble squares on a dais 5 high, ringed by sandstone stairs
// that climb onto it from every side (rounded corners). Its location is the rim's north-west corner at the ground's Z.
// Double-click any part of it to start a game or to see the one being played. White sits on the south side (the camera's
// side), Black on the north. The layout follows Battle Chess by Arya, kept up by dragon slayer2 (reference only).

using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;

namespace Server.BattleChess;

[SerializationGenerator(0, false)]
public partial class BattleChessBoard : BaseAddon
{
    public const int DarkSquare = 0x050F;   // teal marble
    public const int LightSquare = 0x0512;  // grey marble
    public const int DaisHeight = 5;

    // Sandstone stairs: each climbs toward the board (guide 4.2: a step named for a direction climbs that way).
    public const int StairNorthRow = 0x076F; // climbs south
    public const int StairSouthRow = 0x076D; // climbs north
    public const int StairWestCol = 0x0770;  // climbs east
    public const int StairEastCol = 0x076E;  // climbs west
    public const int CornerNW = 0x0775;
    public const int CornerNE = 0x0778;
    public const int CornerSW = 0x0777;
    public const int CornerSE = 0x0776;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ChessSet _set = ChessSet.Classic;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _where = "the Haven inn's yard";

    [Constructible]
    public BattleChessBoard()
    {
        for (var i = 0; i < 10; i++)
        {
            for (var j = 0; j < 10; j++)
            {
                if (i is 0 or 9 || j is 0 or 9)
                {
                    AddComponent(new AddonComponent(RimPiece(i, j)) { Name = "the chessboard's steps" }, i, j, 0);
                }
                else
                {
                    var sq = new Square(i - 1, 8 - j);
                    AddComponent(new AddonComponent(IsDark(sq) ? DarkSquare : LightSquare) { Name = $"square {sq}" }, i, j, DaisHeight);
                }
            }
        }
    }

    public static bool IsDark(Square s) => (s.File + s.Rank) % 2 == 0;

    public static int RimPiece(int i, int j) => (i, j) switch
    {
        (0, 0) => CornerNW,
        (9, 0) => CornerNE,
        (0, 9) => CornerSW,
        (9, 9) => CornerSE,
        (_, 0) => StairNorthRow,
        (_, 9) => StairSouthRow,
        (0, _) => StairWestCol,
        _      => StairEastCol
    };

    public override string DefaultName => "a Battle Chess board";

    /// <summary>The game being played here, if any. Never saved: a restart ends it.</summary>
    public BattleChessGame Game { get; set; }

    /// <summary>The world tile of a square (on the dais).</summary>
    public Point3D ToWorld(Square s) => new(X + 1 + s.File, Y + 8 - s.Rank, Z + DaisHeight);

    /// <summary>The square under a world tile; an invalid square off the board.</summary>
    public Square ToSquare(int x, int y)
    {
        var s = new Square(x - X - 1, 8 - (y - Y));
        return s.Valid ? s : new Square(-1, -1);
    }

    public Point3D Center => new(X + 5, Y + 5, Z + DaisHeight);

    public override void OnComponentUsed(AddonComponent c, Mobile from) => OnUsed(from);

    public override void OnDoubleClick(Mobile from) => OnUsed(from);

    public void OnUsed(Mobile from)
    {
        if (from.Map != Map || !from.InRange(Center, BattleChessSettings.Current.StartRange + 6))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (Game != null && Game.IsPlayer(from) && Game.Phase != GamePhase.Over)
        {
            Game.Resend(from);
            return;
        }

        from.CloseGump<BattleChessBoardGump>();
        from.SendGump(new BattleChessBoardGump(from, this, Set));
    }

    public override void OnAfterDelete()
    {
        Game?.Abort("The board was taken away.");
        base.OnAfterDelete();
    }
}
