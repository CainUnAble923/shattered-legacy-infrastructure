// BattleChessVerification.cs
//
// cc-P862: Battle Chess. Notes in shard-migration notes/cc-P862-battle-chess.md.
//
// Facts: the rules engine counts exactly the published move-path numbers (perft) of four standard test positions, which
// covers castling, en passant and promotion; a refused move says why; checkmate, stalemate, the fifty-move rule and bare
// kings end the game; castling, en passant and promotion move the right pieces. In the world: the board is a 10 x 10 addon
// with whole stairs and the squares where they belong; it places itself once; a game invites, draws colours, sets up 32
// living pieces facing their way, refuses a move out of turn, plays to checkmate, books the result and frees the board; a
// capture removes the taken piece; a pawn promotes to the player's choice; resigning, running out of time and staying away
// lose; a piece deleted by staff stops the game with no result; nothing is given but the record; the records save and load
// and an unknown version is refused; the saved board loads.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.BattleChess;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BattleChessVerification : IDisposable
{
    private static readonly Point3D Spot = new(5470, 3870, 0);
    private readonly ITestOutputHelper _out;
    private readonly List<Item> _items = [];
    private readonly List<Mobile> _mobiles = [];

    public BattleChessVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        BattleChessGame.ResetForTests();
        BattleChessRecords.ResetForTests();
        BattleChessGame.Instant = true;
        BattleChessGame.InviterIsWhite = () => true;
        BattleChessSettings.Current = new BattleChessSettings { X = Spot.X, Y = Spot.Y, Z = Spot.Z, ClearSeconds = 0 };
    }

    public void Dispose()
    {
        BattleChessGame.ResetForTests();
        BattleChessRecords.ResetForTests();
        BattleChessSettings.Current = new BattleChessSettings();

        foreach (var item in _items)
        {
            item.Delete();
        }

        foreach (var m in _mobiles)
        {
            m.NetState = null;
            m.Delete();
        }
    }

    // ---- the rules

    private static long Perft(ChessPosition p, int depth)
    {
        if (depth == 0)
        {
            return 1;
        }

        long n = 0;
        foreach (var m in p.LegalMoves())
        {
            PieceKind[] kinds = m.Promotes ? [PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight] : [m.Promotion];
            foreach (var k in kinds)
            {
                m.Promotion = k;
                var next = p.Clone();
                next.Apply(m);
                n += Perft(next, depth - 1);
            }
        }

        return n;
    }

    // The standard test positions (chessprogramming.org "Perft Results"), written as pieces on squares.
    private const string Kiwipete =
        "ra8 ke8 rh8 pa7 pc7 pd7 qe7 pf7 bg7 ba6 nb6 pe6 nf6 pg6 Pd5 Ne5 pb4 Pe4 Nc3 Qf3 ph3 " +
        "Pa2 Pb2 Pc2 Bd2 Be2 Pf2 Pg2 Ph2 Ra1 Ke1 Rh1 K Q k q";

    private const string Position3 = "pc7 pd6 Ka5 Pb5 rh5 Rb4 pf4 kh4 Pe2 Pg2";

    private const string Position4 =
        "ra8 ke8 rh8 Pa7 pb7 pc7 pd7 pf7 pg7 ph7 bb6 nf6 bg6 Nh6 na5 Pb5 Ba4 Bb4 Pc4 Pe4 qa3 Nf3 " +
        "Pa2 pb2 Pd2 Pg2 Ph2 Ra1 Qd1 Rf1 Kg1 k q";

    [Fact]
    public void TheRulesCountThePublishedMovePaths()
    {
        var start = ChessPosition.Initial();
        Assert.Equal(20, Perft(start, 1));
        Assert.Equal(400, Perft(start, 2));
        Assert.Equal(8902, Perft(start, 3));

        var kiwi = ChessPosition.FromText(Kiwipete);
        Assert.Equal(48, Perft(kiwi, 1));
        Assert.Equal(2039, Perft(kiwi, 2));

        var p3 = ChessPosition.FromText(Position3);
        Assert.Equal(14, Perft(p3, 1));
        Assert.Equal(191, Perft(p3, 2));
        Assert.Equal(2812, Perft(p3, 3));

        var p4 = ChessPosition.FromText(Position4);
        Assert.Equal(6, Perft(p4, 1));
        Assert.Equal(264, Perft(p4, 2));
    }

    [Fact]
    public void ARefusedMoveSaysWhy()
    {
        var p = ChessPosition.Initial();
        string Why(string from, string to)
        {
            Assert.Null(p.FindMove(Square.Parse(from), Square.Parse(to), out var why));
            _out.WriteLine($"{from}-{to}: {why}");
            return why;
        }

        Assert.Equal("There is no piece there.", Why("e4", "e5"));
        Assert.Contains("it is White's move", Why("e7", "e5"));
        Assert.Contains("knight moves in an L", Why("g1", "g3"));
        Assert.Contains("can't take your own piece", Why("d1", "d2"));
        Assert.Contains("pawn moves one square forward", Why("e2", "e5"));
        Assert.Contains("bishop moves diagonally", Why("c1", "c3"));

        // Into check: the knight is pinned to its king by a bishop.
        var pin = ChessPosition.FromText("Ke1 Nd2 bb4 kh8");
        Assert.Null(pin.FindMove(Square.Parse("d2"), Square.Parse("f3"), out var pinned));
        Assert.Contains("leave your king in check", pinned);

        // In check, a move that does not answer it.
        var check = ChessPosition.FromText("Ke1 Pa2 re8 kh8");
        Assert.Null(check.FindMove(Square.Parse("a2"), Square.Parse("a3"), out var inCheck));
        Assert.Contains("Your king is in check", inCheck);

        Assert.NotNull(p.FindMove(Square.Parse("g1"), Square.Parse("f3"), out _));
    }

    [Fact]
    public void CheckmateStalemateAndTheDrawsEndTheGame()
    {
        var p = ChessPosition.Initial();
        foreach (var (f, t) in new[] { ("f2", "f3"), ("e7", "e5"), ("g2", "g4") })
        {
            p.Apply(p.FindMove(Square.Parse(f), Square.Parse(t), out _));
        }

        Assert.Equal(ChessStatus.Playing, p.Status());
        p.Apply(p.FindMove(Square.Parse("d8"), Square.Parse("h4"), out _));
        Assert.Equal(ChessStatus.Checkmate, p.Status());
        Assert.True(ChessRules.IsOver(p.Status()));

        Assert.Equal(ChessStatus.Check, ChessPosition.FromText("Ke1 re8 kh8").Status());
        Assert.Equal(ChessStatus.Stalemate, ChessPosition.FromText("kh8 Qg6 Kf7 black").Status());
        Assert.Equal(ChessStatus.DrawBareKings, ChessPosition.FromText("Ke1 Nb1 ke8").Status());
        Assert.Equal(ChessStatus.Playing, ChessPosition.FromText("Ke1 Ra1 ke8").Status());

        // Fifty moves each of knights hopping out and back, no pawn moved and nothing taken.
        var f50 = ChessPosition.Initial();
        string[] dance = ["g1f3", "g8f6", "f3g1", "f6g8"];
        for (var i = 0; i < 100; i++)
        {
            var d = dance[i % 4];
            Assert.Equal(ChessStatus.Playing, f50.Status());
            f50.Apply(f50.FindMove(Square.Parse(d[..2]), Square.Parse(d[2..]), out _));
        }

        Assert.Equal(100, f50.HalfMoves);
        Assert.Equal(ChessStatus.DrawFiftyMoves, f50.Status());
    }

    [Fact]
    public void CastlingEnPassantAndPromotionMoveTheRightPieces()
    {
        // The king onto its rook, or two squares toward it, castles; the rook jumps over.
        var c = ChessPosition.FromText("Ke1 Ra1 Rh1 ke8 K Q");
        var oo = c.FindMove(Square.Parse("e1"), Square.Parse("h1"), out _);
        Assert.True(oo.Castle);
        Assert.Equal(Square.Parse("g1"), oo.To);
        c.Apply(oo);
        Assert.Equal(PieceKind.King, c[Square.Parse("g1")]!.Value.Kind);
        Assert.Equal(PieceKind.Rook, c[Square.Parse("f1")]!.Value.Kind);
        Assert.Null(c[Square.Parse("h1")]);
        Assert.False(c.WhiteKingSide || c.WhiteQueenSide);

        var ooo = ChessPosition.FromText("Ke1 Ra1 ke8 Q").FindMove(Square.Parse("e1"), Square.Parse("c1"), out _);
        Assert.True(ooo.Castle);
        Assert.Equal(Square.Parse("d1"), ooo.RookTo);

        // Not through an attacked square, and not once the king has moved.
        Assert.Null(ChessPosition.FromText("Ke1 Rh1 ke8 rf8 K").FindMove(Square.Parse("e1"), Square.Parse("g1"), out var through));
        Assert.Contains("can't castle", through);
        var moved = ChessPosition.FromText("Ke1 Rh1 ke8 K");
        moved.Apply(moved.FindMove(Square.Parse("e1"), Square.Parse("e2"), out _));
        moved.Apply(moved.FindMove(Square.Parse("e8"), Square.Parse("d8"), out _));
        moved.Apply(moved.FindMove(Square.Parse("e2"), Square.Parse("e1"), out _));
        moved.Apply(moved.FindMove(Square.Parse("d8"), Square.Parse("e8"), out _));
        Assert.Null(moved.FindMove(Square.Parse("e1"), Square.Parse("g1"), out _));

        // En passant: right after the double step, and only then.
        var ep = ChessPosition.FromText("Ke1 Pe5 ke8 pd7 black");
        ep.Apply(ep.FindMove(Square.Parse("d7"), Square.Parse("d5"), out _));
        Assert.Equal(Square.Parse("d6"), ep.EnPassantSquare);
        var take = ep.FindMove(Square.Parse("e5"), Square.Parse("d6"), out _);
        Assert.True(take.EnPassant);
        Assert.Equal(Square.Parse("d5"), take.CapturedAt);
        ep.Apply(take);
        Assert.Null(ep[Square.Parse("d5")]);
        Assert.Equal(PieceKind.Pawn, ep[Square.Parse("d6")]!.Value.Kind);

        // Promotion: the player's choice, a queen when nobody chose.
        var pr = ChessPosition.FromText("Ke1 Pa7 kh8");
        var up = pr.FindMove(Square.Parse("a7"), Square.Parse("a8"), out _);
        Assert.True(up.Promotes);
        Assert.Equal(PieceKind.Queen, up.Promotion);
        up.Promotion = PieceKind.Knight;
        pr.Apply(up);
        Assert.Equal(new ChessPiece(ChessSide.White, PieceKind.Knight), pr[Square.Parse("a8")]);
    }

    // ---- the world

    private T Track<T>(T item) where T : Item
    {
        _items.Add(item);
        return item;
    }

    private BattleChessBoard Board()
    {
        var board = Track(new BattleChessBoard());
        board.MoveToWorld(Spot, Map.Trammel);
        return board;
    }

    private PlayerMobile Player(string name, int dx = 5, int dy = 11)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = name };
        _mobiles.Add(pm);
        pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y + dy, Spot.Z), Map.Trammel);
        pm.NetState = PacketTestUtilities.CreateTestNetState();
        return pm;
    }

    private (BattleChessGame Game, BattleChessBoard Board, PlayerMobile White, PlayerMobile Black) Start()
    {
        var board = Board();
        var a = Player("P862 Ash");
        var b = Player("P862 Birch", 6);
        var game = BattleChessGame.Open(board, a, ChessSet.Classic, out var refusal);
        Assert.True(game != null, refusal);
        Assert.True(game.Invite(b, out refusal), refusal);
        Assert.True(game.Accept(b, out refusal), refusal);
        return (game, board, a, b);
    }

    private static void Play(BattleChessGame g, Mobile who, string move)
    {
        Assert.True(g.TryPlay(who, Square.Parse(move[..2]), Square.Parse(move[2..4]), out var refusal), refusal);
    }

    [Fact]
    public void TheBoardIsWholeAndMapsItsSquares()
    {
        var board = Board();
        Assert.Equal(100, board.Components.Count);

        var stairs = board.Components.Where(c => c.Z == Spot.Z).ToList();
        Assert.Equal(36, stairs.Count);
        Assert.Equal(64, board.Components.Count(c => c.Z == Spot.Z + BattleChessBoard.DaisHeight));

        // The four rounded corners at the corners, and every side's stair climbing toward the board.
        int At(int dx, int dy) => board.Components.Single(c => c.X == Spot.X + dx && c.Y == Spot.Y + dy).ItemID;
        Assert.Equal(BattleChessBoard.CornerNW, At(0, 0));
        Assert.Equal(BattleChessBoard.CornerNE, At(9, 0));
        Assert.Equal(BattleChessBoard.CornerSW, At(0, 9));
        Assert.Equal(BattleChessBoard.CornerSE, At(9, 9));
        Assert.Equal(BattleChessBoard.StairNorthRow, At(4, 0));
        Assert.Equal(BattleChessBoard.StairSouthRow, At(4, 9));
        Assert.Equal(BattleChessBoard.StairWestCol, At(0, 4));
        Assert.Equal(BattleChessBoard.StairEastCol, At(9, 4));

        for (var f = 0; f < 8; f++)
        {
            for (var r = 0; r < 8; r++)
            {
                var sq = new Square(f, r);
                var w = board.ToWorld(sq);
                Assert.Equal(sq, board.ToSquare(w.X, w.Y));
                Assert.Equal(BattleChessBoard.IsDark(sq) ? BattleChessBoard.DarkSquare : BattleChessBoard.LightSquare, At(w.X - Spot.X, w.Y - Spot.Y));
            }
        }

        // a1 is dark and in the south-west; White's back rank is the south row.
        Assert.Equal(new Point3D(Spot.X + 1, Spot.Y + 8, Spot.Z + 5), board.ToWorld(Square.Parse("a1")));
        Assert.Equal(new Point3D(Spot.X + 8, Spot.Y + 1, Spot.Z + 5), board.ToWorld(Square.Parse("h8")));
        Assert.True(BattleChessBoard.IsDark(Square.Parse("a1")));
        Assert.False(board.ToSquare(Spot.X, Spot.Y).Valid);
    }

    [Fact]
    public void TheBoardPlacesItselfOnce()
    {
        Assert.Null(BattleChessSeeder.Find());
        Assert.True(BattleChessSeeder.Seed());
        var board = BattleChessSeeder.Find();
        Assert.NotNull(board);
        Track(board);
        Assert.Equal(Spot, board.Location);
        Assert.False(BattleChessSeeder.Seed());
        Assert.Same(board, BattleChessSeeder.Find());

        BattleChessSettings.Current.Enabled = false;
        board.Delete();
        Assert.False(BattleChessSeeder.Seed());
        Assert.Null(BattleChessSeeder.Find());
    }

    [Fact]
    public void AGameSetsUpLivingPiecesAndTakesTurns()
    {
        var (g, board, white, black) = Start();
        Assert.Equal(GamePhase.Playing, g.Phase);
        Assert.Same(white, g.White);
        Assert.Same(black, g.Black);
        Assert.Same(g, board.Game);
        Assert.Equal(32, g.Pieces.Count);

        foreach (var (sq, piece) in g.Pieces)
        {
            Assert.Equal(board.ToWorld(sq), piece.Location);
            Assert.Equal(g.Position[sq]!.Value, piece.Piece);
            Assert.Equal(piece.Piece.Side == ChessSide.White ? Direction.North : Direction.South, piece.Direction & Direction.Mask);
            Assert.True(piece.Blessed);
            Assert.False(piece.Player);
        }

        Assert.IsType<BattleChessTarget>(white.Target);
        Assert.False(g.TryPlay(black, Square.Parse("e7"), Square.Parse("e5"), out var refusal));
        Assert.Equal("It is not your move.", refusal);
        Assert.False(g.TryPlay(white, Square.Parse("e2"), Square.Parse("e5"), out refusal));
        Assert.Contains("pawn moves one square forward", refusal);

        Play(g, white, "e2e4");
        var pawn = g.Pieces[Square.Parse("e4")];
        Assert.Equal(board.ToWorld(Square.Parse("e4")), pawn.Location);
        Assert.False(g.Pieces.ContainsKey(Square.Parse("e2")));
        Assert.IsType<BattleChessTarget>(black.Target);

        // A second game can't start on a busy board, and a player can't be in two games.
        var other = Player("P862 Cedar", 7);
        Assert.Null(BattleChessGame.Open(board, other, ChessSet.Undead, out refusal));
        Assert.Contains("already being played", refusal);
        Assert.NotNull(BattleChessGame.InGame(white));
    }

    [Fact]
    public void CheckmateEndsTheGameBooksItAndFreesTheBoard()
    {
        var (g, board, white, black) = Start();
        var gold = white.Backpack?.TotalGold ?? 0;
        Play(g, white, "f2f3");
        Play(g, black, "e7e5");
        Play(g, white, "g2g4");
        Play(g, black, "d8h4");

        Assert.Equal(GamePhase.Over, g.Phase);
        Assert.Same(black, g.Winner);
        Assert.Contains("checkmate", g.Result);
        Assert.Equal(1, BattleChessRecords.Of(black).Wins);
        Assert.Equal(1, BattleChessRecords.Of(white).Losses);
        Assert.Empty(g.Pieces);
        Assert.Null(board.Game);
        Assert.Null(BattleChessGame.InGame(white));
        Assert.Equal(gold, white.Backpack?.TotalGold ?? 0);

        // The board is free for the next game.
        var next = BattleChessGame.Open(board, white, ChessSet.Animals, out var refusal);
        Assert.True(next != null, refusal);
    }

    [Fact]
    public void ACaptureRemovesTheTakenPiece()
    {
        var (g, _, white, black) = Start();
        Play(g, white, "e2e4");
        Play(g, black, "d7d5");
        var victim = g.Pieces[Square.Parse("d5")];
        var taker = g.Pieces[Square.Parse("e4")];
        Play(g, white, "e4d5");

        Assert.True(victim.Deleted);
        Assert.Same(taker, g.Pieces[Square.Parse("d5")]);
        Assert.Equal(31, g.Pieces.Count);
        Assert.Equal(new[] { new ChessPiece(ChessSide.Black, PieceKind.Pawn) }, g.TakenFromBlack);
        Assert.Equal(GamePhase.Playing, g.Phase);
    }

    [Fact]
    public void APawnPromotesToThePlayersChoice()
    {
        var (g, board, white, black) = Start();
        Assert.True(g.LoadPosition(ChessPosition.FromText("Ke1 Pa7 Pb2 kh8")));
        Assert.Equal(4, g.Pieces.Count);

        Play(g, white, "a7a8");
        Assert.Equal(GamePhase.Promoting, g.Phase);
        Assert.False(g.Promote(black, PieceKind.Queen));
        Assert.True(g.Promote(white, PieceKind.Knight));

        var knight = g.Pieces[Square.Parse("a8")];
        Assert.Equal(new ChessPiece(ChessSide.White, PieceKind.Knight), knight.Piece);
        Assert.Equal(ChessSets.Look(ChessSet.Classic, PieceKind.Knight).Body, knight.Body.BodyID);
        Assert.Equal(board.ToWorld(Square.Parse("a8")), knight.Location);
        Assert.Equal(GamePhase.Playing, g.Phase);
        Assert.Same(black, g.ToMove);
    }

    [Fact]
    public void ResigningRunningOutOfTimeAndStayingAwayLose()
    {
        var (g, _, white, black) = Start();
        g.Resign(white);
        Assert.Same(black, g.Winner);
        Assert.Contains("resigns", g.Result);

        var now = DateTime.UtcNow;
        BattleChessGame.Clock = () => now;
        var (t, _, w2, b2) = StartAgain();
        Play(t, w2, "e2e4");
        now += TimeSpan.FromMinutes(BattleChessSettings.Current.MoveMinutes);
        t.Tick();
        Assert.Same(w2, t.Winner);
        Assert.Contains("on time", t.Result);

        var (a, _, w3, b3) = StartAgain();
        w3.NetState = null;
        a.Tick();
        Assert.Equal(GamePhase.Playing, a.Phase);
        now += TimeSpan.FromMinutes(BattleChessSettings.Current.AwayMinutes);
        a.Tick();
        Assert.Same(b3, a.Winner);
        Assert.Contains("did not come back", a.Result);

        Assert.Equal(1, BattleChessRecords.Of(black).Wins);
        Assert.Equal(1, BattleChessRecords.Of(w2).Wins);
        Assert.Equal(1, BattleChessRecords.Of(w3).Losses);
    }

    private (BattleChessGame Game, BattleChessBoard Board, PlayerMobile White, PlayerMobile Black) StartAgain()
    {
        foreach (var g in BattleChessGame.Games.ToList())
        {
            g.Abort(null);
        }

        return Start();
    }

    [Fact]
    public void InvitationsAreRefusedPlainly()
    {
        var board = Board();
        var a = Player("P862 Elm");
        var far = Player("P862 Fir", 40, 40);
        var game = BattleChessGame.Open(board, a, ChessSet.Fantasy, out var refusal);
        Assert.NotNull(game);

        Assert.False(game.Invite(a, out refusal));
        Assert.Contains("yourself", refusal);
        Assert.False(game.Invite(far, out refusal));
        Assert.Contains("by the board", refusal);
        Assert.Null(BattleChessGame.Open(board, far, ChessSet.Fantasy, out refusal));

        // A refusal frees the board; an unanswered invitation lapses.
        var b = Player("P862 Gorse", 6);
        Assert.True(game.Invite(b, out _));
        game.Refuse(b);
        Assert.Null(board.Game);

        var now = DateTime.UtcNow;
        BattleChessGame.Clock = () => now;
        var lapse = BattleChessGame.Open(board, a, ChessSet.Fantasy, out _);
        Assert.True(lapse.Invite(b, out _));
        now += TimeSpan.FromMinutes(BattleChessSettings.Current.InviteMinutes);
        lapse.Tick();
        Assert.Equal(GamePhase.Over, lapse.Phase);
        Assert.Null(board.Game);
        Assert.Empty(BattleChessRecords.Records);
    }

    [Fact]
    public void APieceDeletedByStaffStopsTheGameWithNoResult()
    {
        var (g, board, white, _) = Start();
        g.Pieces[Square.Parse("b1")].Delete();
        Assert.Equal(GamePhase.Over, g.Phase);
        Assert.Null(g.Winner);
        Assert.Empty(g.Pieces);
        Assert.Null(board.Game);
        Assert.Empty(BattleChessRecords.Records);
        Assert.Null(white.Target as BattleChessTarget);
    }

    [Fact]
    public void TheRecordsAndTheBoardSaveAndLoad()
    {
        var a = Player("P862 Hazel");
        var b = Player("P862 Ivy", 6);
        BattleChessRecords.Book(a, b);
        BattleChessRecords.Book(null, null, a, b);

        var records = new BattleChessRecords();
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        records.Serialize(writer);
        writer.Flush();
        BattleChessRecords.ResetForTests();
        records.Deserialize(new BufferReader(buffer));
        Assert.Equal(1, BattleChessRecords.Of(a).Wins);
        Assert.Equal(1, BattleChessRecords.Of(a).Draws);
        Assert.Equal(1, BattleChessRecords.Of(b).Losses);
        Assert.Equal("1 won, 0 lost, 1 drawn", BattleChessRecords.Of(a).ToString());

        var bad = new byte[16];
        var w2 = new BufferWriter(bad, true);
        w2.WriteEncodedInt(BattleChessRecords.CurrentVersion + 1);
        w2.Flush();
        Assert.Throws<InvalidDataException>(() => records.Deserialize(new BufferReader(bad)));

        var board = Track(new BattleChessBoard { Set = ChessSet.Undead, Where = "a test yard" });
        var buf = new byte[4096];
        var bw = new BufferWriter(buf, true);
        board.Serialize(bw);
        bw.Flush();
        var copy = (BattleChessBoard)Activator.CreateInstance(typeof(BattleChessBoard), board.Serial)!;
        copy.Deserialize(new BufferReader(buf));
        Assert.Equal(ChessSet.Undead, copy.Set);
        Assert.Equal("a test yard", copy.Where);
    }

    [Fact]
    public void TheSettingsFileIsRefusedWholeWhenWrong()
    {
        var errors = new List<string>();
        Assert.NotNull(BattleChessSettings.Parse(BattleChessSettings.ToJson(new BattleChessSettings()), errors));
        Assert.Empty(errors);

        Assert.Null(BattleChessSettings.Parse("{ \"Map\": \"Nowhere\", \"MoveMinutes\": 0 }", errors));
        Assert.Equal(2, errors.Count);
        Assert.Null(BattleChessSettings.Parse("not json", errors));
    }
}
