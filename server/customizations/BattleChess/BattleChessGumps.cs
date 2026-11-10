// BattleChessGumps.cs
//
// cc-P862: the windows of Battle Chess. The board's window (choose a set, invite a player, or watch the game being played),
// the invitation, the game bar each player keeps open, the pawn's promotion and the rules. Every button goes through a
// public Respond so the facts can press it without a client.

using System.Linq;
using System.Text;
using Server.Gumps;
using Server.Network;

namespace Server.BattleChess;

public class BattleChessBoardGump : Gump
{
    public const int BtnInvite = 1;
    public const int BtnHelp = 2;
    public const int BtnSetBase = 10;

    private readonly BattleChessBoard _board;
    private readonly ChessSet _set;

    public BattleChessBoardGump(Mobile viewer, BattleChessBoard board, ChessSet set) : base(120, 80)
    {
        _board = board;
        _set = set;
        var game = board.Game;

        AddBackground(0, 0, 520, 330, 9270);
        AddImageTiled(14, 14, 492, 302, 2624);
        AddAlphaRegion(14, 14, 492, 302);
        AddLabel(30, 22, 0x386, "Battle Chess");
        AddLabel(150, 22, 0x3B2, "Free to play: no stakes, no prize");

        if (game != null && game.Phase != GamePhase.Inviting)
        {
            AddHtml(30, 52, 460, 200, Watch(game), false, false);
        }
        else if (game != null)
        {
            AddHtml(30, 52, 460, 60, $"<BASEFONT COLOR=#FFFFFF>{game.Owner.Name} is setting up a game. Come back in a moment.</BASEFONT>", false, false);
        }
        else
        {
            AddLabel(30, 52, 1153, "Choose the pieces, then invite a player standing by the board.");
            for (var i = 0; i < 5; i++)
            {
                var s = (ChessSet)i;
                AddButton(30, 80 + i * 26, s == set ? 2154 : 2151, 2154, BtnSetBase + i);
                AddLabelCropped(64, 82 + i * 26, 440, 20, s == set ? 0x386 : 1153, ChessSets.Describe(s));
            }

            AddButton(30, 222, 4005, 4007, BtnInvite);
            AddLabel(64, 224, 1153, "Invite a player");
        }

        AddButton(30, 252, 4011, 4013, BtnHelp);
        AddLabel(64, 254, 1153, "How to play");
        AddLabel(30, 284, 0x3B2, $"Your record: {BattleChessRecords.Of(viewer)}");
    }

    private static string Watch(BattleChessGame g)
    {
        var sb = new StringBuilder("<BASEFONT COLOR=#FFFFFF>");
        sb.Append($"<B>{g.White?.Name}</B> (White) against <B>{g.Black?.Name}</B> (Black).<BR>");
        if (g.Phase == GamePhase.Over)
        {
            sb.Append(g.Result);
        }
        else
        {
            sb.Append($"{ChessRules.SideName(g.Position.ToMove)} to move, move {g.Position.FullMoves}.");
            if (g.LastMove != null)
            {
                sb.Append($" Last move: {g.LastMove}.");
            }
        }

        sb.Append("<BR><BR>Anyone may watch; the board is free again when the game ends.</BASEFONT>");
        return sb.ToString();
    }

    public override void OnResponse(NetState sender, in RelayInfo info) => Respond(sender.Mobile, info.ButtonID);

    public void Respond(Mobile m, int id)
    {
        if (id >= BtnSetBase && id < BtnSetBase + 5)
        {
            _board.Set = (ChessSet)(id - BtnSetBase);
            m.SendGump(new BattleChessBoardGump(m, _board, _board.Set));
        }
        else if (id == BtnHelp)
        {
            m.CloseGump<BattleChessHelpGump>();
            m.SendGump(new BattleChessHelpGump());
        }
        else if (id == BtnInvite)
        {
            var game = BattleChessGame.Open(_board, m, _set, out var refusal);
            if (game == null)
            {
                m.SendMessage(BattleChessGame.WarnHue, refusal);
                return;
            }

            m.Target = new BattleChessInviteTarget(game);
            m.SendMessage(BattleChessGame.Hue, "Choose the player you want to play against.");
        }
    }
}

public class BattleChessInviteGump : Gump
{
    public const int BtnAccept = 1;
    public const int BtnRefuse = 2;

    private readonly BattleChessGame _game;

    public BattleChessInviteGump(BattleChessGame game) : base(160, 120)
    {
        _game = game;
        Closable = false;

        AddBackground(0, 0, 340, 150, 9270);
        AddImageTiled(14, 14, 312, 122, 2624);
        AddAlphaRegion(14, 14, 312, 122);
        AddLabel(30, 22, 0x386, "Battle Chess");
        AddHtml(30, 46, 290, 44,
            $"<BASEFONT COLOR=#FFFFFF>{game.Owner.Name} invites you to a game with the {game.Set} pieces. The colours are drawn when you accept.</BASEFONT>",
            false, false);
        AddButton(30, 100, 4023, 4025, BtnAccept);
        AddLabel(64, 102, 1153, "Accept");
        AddButton(170, 100, 4017, 4019, BtnRefuse);
        AddLabel(204, 102, 1153, "Refuse");
    }

    public override void OnResponse(NetState sender, in RelayInfo info) => Respond(sender.Mobile, info.ButtonID);

    public void Respond(Mobile m, int id)
    {
        if (id == BtnAccept)
        {
            if (!_game.Accept(m, out var refusal))
            {
                m.SendMessage(BattleChessGame.WarnHue, refusal);
            }
        }
        else
        {
            _game.Refuse(m);
        }
    }
}

public class BattleChessGameGump : Gump
{
    public const int BtnMove = 1;
    public const int BtnResign = 2;
    public const int BtnHelp = 3;

    private readonly BattleChessGame _game;

    public BattleChessGameGump(BattleChessGame game, Mobile viewer, string note) : base(40, 40)
    {
        _game = game;
        var side = game.SideOf(viewer);
        var over = game.Phase == GamePhase.Over;
        var yourMove = !over && viewer == game.ToMove;

        AddBackground(0, 0, 460, 168, 9270);
        AddImageTiled(14, 14, 432, 140, 2624);
        AddAlphaRegion(14, 14, 432, 140);
        AddLabel(30, 20, 0x386, $"Battle Chess: you play {ChessRules.SideName(side)} against {game.Opponent(viewer)?.Name}");

        string line;
        if (over)
        {
            line = game.Result ?? "The game is over.";
        }
        else if (game.Phase == GamePhase.Moving)
        {
            line = "The piece is making its move...";
        }
        else if (game.Phase == GamePhase.Promoting)
        {
            line = yourMove ? "Choose what your pawn becomes." : "Your opponent is promoting a pawn.";
        }
        else if (yourMove)
        {
            line = game.Position.InCheck(game.Position.ToMove) ? "Your king is in check! Your move." : "Your move.";
        }
        else
        {
            line = $"Waiting for {game.ToMove?.Name} to move.";
        }

        AddHtml(30, 42, 400, 36, $"<BASEFONT COLOR=#FFFFFF>{line}</BASEFONT>", false, false);
        if (note != null && note != line)
        {
            AddLabelCropped(30, 78, 400, 20, 0x35, note);
        }

        AddLabelCropped(30, 98, 400, 20, 0x3B2,
            $"Move {game.Position.FullMoves}. Taken by White: {Taken(game.TakenFromBlack)}. Taken by Black: {Taken(game.TakenFromWhite)}.");

        if (yourMove && game.Phase == GamePhase.Playing)
        {
            AddButton(30, 124, 4005, 4007, BtnMove);
            AddLabel(64, 126, 1153, "Move");
        }

        if (!over)
        {
            AddButton(150, 124, 4017, 4019, BtnResign);
            AddLabel(184, 126, 1153, "Resign");
        }

        AddButton(280, 124, 4011, 4013, BtnHelp);
        AddLabel(314, 126, 1153, "How to play");
    }

    private static string Taken(System.Collections.Generic.List<ChessPiece> list) =>
        list.Count == 0
            ? "nothing"
            : string.Join(" ", list.GroupBy(p => p.Kind).OrderByDescending(g => ChessRules.Value(g.Key))
                .Select(g => g.Count() > 1 ? $"{g.Count()} {ChessRules.Name(g.Key)}s" : $"a {ChessRules.Name(g.Key)}"));

    public override void OnResponse(NetState sender, in RelayInfo info) => Respond(sender.Mobile, info.ButtonID);

    public void Respond(Mobile m, int id)
    {
        switch (id)
        {
            case BtnMove:
                _game.Resend(m);
                break;
            case BtnResign:
                m.CloseGump<BattleChessResignGump>();
                m.SendGump(new BattleChessResignGump(_game));
                break;
            case BtnHelp:
                m.CloseGump<BattleChessHelpGump>();
                m.SendGump(new BattleChessHelpGump());
                m.SendGump(new BattleChessGameGump(_game, m, null));
                break;
        }
    }
}

public class BattleChessResignGump : Gump
{
    public const int BtnYes = 1;

    private readonly BattleChessGame _game;

    public BattleChessResignGump(BattleChessGame game) : base(200, 160)
    {
        _game = game;
        AddBackground(0, 0, 280, 110, 9270);
        AddImageTiled(14, 14, 252, 82, 2624);
        AddAlphaRegion(14, 14, 252, 82);
        AddLabel(30, 24, 0x386, "Resign this game?");
        AddButton(30, 60, 4023, 4025, BtnYes);
        AddLabel(64, 62, 1153, "Resign");
        AddButton(150, 60, 4017, 4019, 0);
        AddLabel(184, 62, 1153, "Play on");
    }

    public override void OnResponse(NetState sender, in RelayInfo info) => Respond(sender.Mobile, info.ButtonID);

    public void Respond(Mobile m, int id)
    {
        if (id == BtnYes)
        {
            _game.Resign(m);
        }
    }
}

public class BattleChessPromotionGump : Gump
{
    private static readonly PieceKind[] Choices = [PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight];

    private readonly BattleChessGame _game;

    public BattleChessPromotionGump(BattleChessGame game) : base(200, 140)
    {
        _game = game;
        Closable = false;
        AddBackground(0, 0, 300, 180, 9270);
        AddImageTiled(14, 14, 272, 152, 2624);
        AddAlphaRegion(14, 14, 272, 152);
        AddLabel(30, 22, 0x386, "Your pawn reaches the last rank!");
        AddLabel(30, 44, 1153, "It becomes:");
        for (var i = 0; i < Choices.Length; i++)
        {
            AddButton(30, 70 + i * 24, 4005, 4007, i + 1);
            AddLabel(64, 72 + i * 24, 1153, $"a {ChessRules.Name(Choices[i])}");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info) => Respond(sender.Mobile, info.ButtonID);

    public void Respond(Mobile m, int id) =>
        _game.Promote(m, id is >= 1 and <= 4 ? Choices[id - 1] : PieceKind.Queen);
}

public class BattleChessHelpGump : Gump
{
    public const string Text =
        "<BASEFONT COLOR=#FFFFFF><B>Playing</B><BR>" +
        "Double-click the board, choose a set of pieces and invite a player standing by the board. When they accept, the " +
        "colours are drawn. White starts, and sits on the south side.<BR>" +
        "On your move, target one of your pieces, then the square it goes to. You can also double-click your piece. If you " +
        "lose the cursor, press Move in the game window or double-click the board.<BR><BR>" +
        "<B>The pieces</B><BR>" +
        "A pawn moves one square forward (two from its first square) and takes one square diagonally forward. A pawn that " +
        "has just made its double step can be taken en passant by a pawn beside it, on the very next move. A pawn reaching " +
        "the last rank becomes a queen, rook, bishop or knight: you choose.<BR>" +
        "A knight moves in an L and jumps over pieces. A bishop moves diagonally, a rook in straight lines, a queen both " +
        "ways. A king moves one square.<BR>" +
        "To castle, move your king two squares toward a rook (or onto it). Neither may have moved, the squares between " +
        "must be empty, and the king may not be in check, cross an attacked square or land in check.<BR><BR>" +
        "<B>Winning</B><BR>" +
        "Checkmate wins. A player with no legal move who is not in check is stalemated: a draw. Fifty moves each without a " +
        "capture or a pawn move is a draw, and so is a board where neither side can mate. You may resign. Not moving for " +
        "ten minutes, or staying logged out for five, loses the game.<BR><BR>" +
        "Battle Chess is free: nothing is staked and nothing is won but your record. After Battle Chess by Arya, kept up " +
        "by dragon slayer2.</BASEFONT>";

    public BattleChessHelpGump() : base(80, 60)
    {
        AddBackground(0, 0, 480, 420, 9270);
        AddImageTiled(14, 14, 452, 392, 2624);
        AddAlphaRegion(14, 14, 452, 392);
        AddLabel(30, 22, 0x386, "How to play Battle Chess");
        AddHtml(30, 48, 420, 344, Text, false, true);
    }
}
