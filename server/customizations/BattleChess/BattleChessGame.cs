// BattleChessGame.cs
//
// cc-P862: one game of Battle Chess on a board: two players, the living pieces, whose turn it is, and the end. Free to play:
// no stakes, no prize; the result goes into each player's record (BattleChessRecords) and nothing else.
//
// The flow follows Battle Chess by Arya, kept up by dragon slayer2 (third-party, reference only; no code taken): a player
// starts a game at the board and targets the player they invite; the guest accepts; the colours are drawn; the player to
// move targets one of their pieces and then the square it goes to; the piece walks there, and on a capture it fights the
// piece it takes. Checkmate wins; stalemate, the fifty-move rule and bare kings draw; a player may resign. A player who does
// not move in time, or stays logged out too long, loses. Nothing is saved: a restart ends the game and clears the pieces.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Gumps;
using Server.Mobiles;
using Server.Targeting;

namespace Server.BattleChess;

public enum GamePhase
{
    Inviting,
    Playing,
    Moving,
    Promoting,
    Over
}

public sealed class BattleChessGame
{
    public const int Hue = 0x59;
    public const int WarnHue = 0x22;

    /// <summary>Who plays White (true: the inviter). The facts replace it.</summary>
    public static Func<bool> InviterIsWhite = Utility.RandomBool;

    /// <summary>When true the pieces jump to their squares at once (no walking, no fight): for the facts.</summary>
    public static bool Instant;

    public static Func<DateTime> Clock = () => Core.Now;

    private static readonly List<BattleChessGame> _games = [];

    public static IReadOnlyList<BattleChessGame> Games => _games;

    private readonly Dictionary<Square, BattleChessPiece> _pieces = new();
    private Timer _tick;
    private ChessMove _pending;
    private DateTime _phaseSince;
    private readonly Dictionary<Mobile, DateTime> _away = new();

    public BattleChessBoard Board { get; }
    public ChessSet Set { get; }
    public Mobile Owner { get; }
    public Mobile Guest { get; private set; }
    public Mobile White { get; private set; }
    public Mobile Black { get; private set; }
    public GamePhase Phase { get; private set; }
    public ChessPosition Position { get; private set; } = ChessPosition.Initial();
    public List<ChessPiece> TakenFromWhite { get; } = [];
    public List<ChessPiece> TakenFromBlack { get; } = [];
    public ChessMove LastMove { get; private set; }
    public string Result { get; private set; }
    public Mobile Winner { get; private set; }

    public IReadOnlyDictionary<Square, BattleChessPiece> Pieces => _pieces;

    private static BattleChessSettings S => BattleChessSettings.Current;

    private BattleChessGame(BattleChessBoard board, Mobile owner, ChessSet set)
    {
        Board = board;
        Owner = owner;
        Set = set;
        Phase = GamePhase.Inviting;
        _phaseSince = Clock();
        _games.Add(this);
        board.Game = this;
        _tick = Timer.DelayCall(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), Tick);
    }

    public static BattleChessGame InGame(Mobile m) => _games.FirstOrDefault(g => g.IsPlayer(m) && g.Phase != GamePhase.Over);

    public bool IsPlayer(Mobile m) => m != null && (m == Owner || m == Guest);

    public Mobile ToMove => Position.ToMove == ChessSide.White ? White : Black;

    public Mobile Opponent(Mobile m) => m == White ? Black : White;

    public ChessSide SideOf(Mobile m) => m == Black ? ChessSide.Black : ChessSide.White;

    private static bool Usable(Mobile m) => m is { Deleted: false, Alive: true } && m.NetState != null;

    // ---- starting

    /// <summary>A player opens a game at the board; returns it, or null and why not.</summary>
    public static BattleChessGame Open(BattleChessBoard board, Mobile owner, ChessSet set, out string refusal)
    {
        refusal = null;
        if (board is not { Deleted: false } || board.Map == null)
        {
            refusal = "That board is gone.";
        }
        else if (board.Game != null)
        {
            refusal = "A game is already being played on this board. Watch, or come back when it ends.";
        }
        else if (owner is not PlayerMobile || !owner.Alive)
        {
            refusal = "Only the living can play.";
        }
        else if (InGame(owner) != null)
        {
            refusal = "You are already in a game.";
        }
        else if (owner.Map != board.Map || !owner.InRange(board.Center, S.StartRange))
        {
            refusal = "Stand by the board to start a game.";
        }

        if (refusal != null)
        {
            return null;
        }

        return new BattleChessGame(board, owner, set);
    }

    /// <summary>The inviter names their opponent.</summary>
    public bool Invite(Mobile guest, out string refusal)
    {
        refusal = null;
        if (Phase != GamePhase.Inviting)
        {
            refusal = "This game has already started.";
        }
        else if (guest is not PlayerMobile || guest == Owner)
        {
            refusal = guest == Owner ? "You can't play against yourself." : "Choose another player.";
        }
        else if (!Usable(guest))
        {
            refusal = "That player can't play right now.";
        }
        else if (InGame(guest) != null)
        {
            refusal = $"{guest.Name} is already in a game.";
        }
        else if (guest.Map != Board.Map || !guest.InRange(Board.Center, S.StartRange))
        {
            refusal = $"{guest.Name} must be by the board to play.";
        }

        if (refusal != null)
        {
            return false;
        }

        Guest = guest;
        _phaseSince = Clock();
        guest.CloseGump<BattleChessInviteGump>();
        guest.SendGump(new BattleChessInviteGump(this));
        Owner.SendMessage(Hue, $"You invite {guest.Name} to a game of Battle Chess. Waiting for an answer...");
        return true;
    }

    public void Refuse(Mobile guest)
    {
        if (guest != Guest || Phase != GamePhase.Inviting)
        {
            return;
        }

        Owner.SendMessage(WarnHue, $"{guest.Name} does not want to play. Double-click the board to invite someone else.");
        Abort(null);
    }

    public bool Accept(Mobile guest, out string refusal)
    {
        refusal = null;
        if (guest != Guest || Phase != GamePhase.Inviting)
        {
            refusal = "That invitation has lapsed.";
            return false;
        }

        if (!Usable(Owner))
        {
            refusal = "The player who invited you has gone.";
            Abort(null);
            return false;
        }

        if (InviterIsWhite())
        {
            White = Owner;
            Black = Guest;
        }
        else
        {
            White = Guest;
            Black = Owner;
        }

        SetUp();
        Phase = GamePhase.Playing;
        _phaseSince = Clock();

        White.SendMessage(Hue, $"You play White against {Black.Name}. White moves first.");
        Black.SendMessage(Hue, $"You play Black against {White.Name}. White moves first.");
        SendTurn();
        return true;
    }

    /// <summary>Starts from a set-up position instead (staff puzzles and the facts): only before the first move.</summary>
    public bool LoadPosition(ChessPosition position)
    {
        if (Phase != GamePhase.Playing || LastMove != null || position.KingOf(ChessSide.White) == null ||
            position.KingOf(ChessSide.Black) == null)
        {
            return false;
        }

        foreach (var p in _pieces.Values.ToList())
        {
            p.Vanish();
        }

        _pieces.Clear();
        Position = position.Clone();
        SetUp();
        SendTurn();
        return true;
    }

    private void SetUp()
    {
        foreach (var (sq, piece) in Position.Pieces())
        {
            Spawn(sq, piece);
        }
    }

    private BattleChessPiece Spawn(Square sq, ChessPiece piece)
    {
        var hue = piece.Side == ChessSide.White ? S.WhiteHue : S.BlackHue;
        var mob = new BattleChessPiece(this, piece, Set, hue);
        mob.Direction = mob.Facing;
        mob.MoveToWorld(Board.ToWorld(sq), Board.Map);
        if (!Instant)
        {
            mob.FixedParticles(0x373A, 1, 15, 5012, hue, 2, EffectLayer.Waist);
        }

        _pieces[sq] = mob;
        return mob;
    }

    // ---- turns

    public void SendTurn()
    {
        if (Phase != GamePhase.Playing)
        {
            return;
        }

        SendGumps(null);
        var mover = ToMove;
        if (Usable(mover))
        {
            mover.Target = new BattleChessTarget(this, mover, true);
            mover.SendMessage(Hue, Position.InCheck(Position.ToMove)
                ? "Your king is in check! Choose the piece to move."
                : "Your move: choose the piece to move.");
        }
    }

    public void SendGumps(string note)
    {
        foreach (var m in (Mobile[])[White, Black])
        {
            if (m is { NetState: not null })
            {
                m.CloseGump<BattleChessGameGump>();
                m.SendGump(new BattleChessGameGump(this, m, note));
            }
        }
    }

    /// <summary>A player who lost their cursor or closed the window gets both back.</summary>
    public void Resend(Mobile m)
    {
        if (Phase == GamePhase.Inviting)
        {
            if (m == Owner && Guest == null)
            {
                m.Target = new BattleChessInviteTarget(this);
                m.SendMessage(Hue, "Choose the player to invite.");
            }
            else if (m == Guest)
            {
                m.SendGump(new BattleChessInviteGump(this));
            }

            return;
        }

        m.CloseGump<BattleChessGameGump>();
        m.SendGump(new BattleChessGameGump(this, m, null));
        if (Phase == GamePhase.Playing && m == ToMove)
        {
            m.Target = new BattleChessTarget(this, m, true);
        }
        else if (Phase == GamePhase.Promoting && m == ToMove)
        {
            m.SendGump(new BattleChessPromotionGump(this));
        }
    }

    /// <summary>The square under whatever was targeted (a piece, a square of the board, or the ground there).</summary>
    public Square SquareOf(object targeted)
    {
        IPoint3D p = targeted switch
        {
            Item item     => item.GetWorldLocation(),
            IPoint3D pt   => pt,
            _             => null
        };

        return p == null ? new Square(-1, -1) : Board.ToSquare(p.X, p.Y);
    }

    public void OnPieceClicked(Mobile from, BattleChessPiece piece)
    {
        var sq = _pieces.FirstOrDefault(kv => kv.Value == piece).Key;
        if (Phase == GamePhase.Playing && from == ToMove && piece.Piece.Side == Position.ToMove)
        {
            from.Target = new BattleChessTarget(this, from, false) { From = sq };
            from.SendMessage(Hue, $"Your {ChessRules.Name(piece.Piece.Kind)} on {sq}: choose the square to move it to.");
            return;
        }

        from.SendMessage($"{piece.Name} on {sq}.");
    }

    /// <summary>
    /// The player moves the piece on one square to another. Refuses with a plain reason (not your turn, not your piece, not a
    /// legal move); a pawn reaching the last rank waits for the player's choice.
    /// </summary>
    public bool TryPlay(Mobile who, Square from, Square to, out string refusal)
    {
        refusal = null;
        if (Phase is not GamePhase.Playing)
        {
            refusal = Phase == GamePhase.Moving ? "Wait for the piece to finish its move." : "No move can be made now.";
            return false;
        }

        if (!IsPlayer(who))
        {
            refusal = "You are not playing this game.";
            return false;
        }

        if (who != ToMove)
        {
            refusal = "It is not your move.";
            return false;
        }

        var move = Position.FindMove(from, to, out refusal);
        if (move == null)
        {
            return false;
        }

        if (move.Promotes)
        {
            _pending = move;
            Phase = GamePhase.Promoting;
            who.CloseGump<BattleChessPromotionGump>();
            who.SendGump(new BattleChessPromotionGump(this));
            Opponent(who)?.SendMessage(Hue, $"{who.Name}'s pawn reaches the last rank and is being promoted...");
            return true;
        }

        Execute(move);
        return true;
    }

    public bool Promote(Mobile who, PieceKind kind)
    {
        if (Phase != GamePhase.Promoting || who != ToMove || _pending == null ||
            kind is PieceKind.Pawn or PieceKind.King)
        {
            return false;
        }

        _pending.Promotion = kind;
        var move = _pending;
        _pending = null;
        Phase = GamePhase.Playing;
        Execute(move);
        return true;
    }

    private void Execute(ChessMove move)
    {
        Phase = GamePhase.Moving;
        _phaseSince = Clock();
        LastMove = move;

        var mover = _pieces[move.From];
        _pieces.Remove(move.From);
        BattleChessPiece victim = null;
        if (move.Captured.HasValue && _pieces.Remove(move.CapturedAt, out var v))
        {
            victim = v;
            (move.Captured.Value.Side == ChessSide.White ? TakenFromWhite : TakenFromBlack).Add(move.Captured.Value);
        }

        BattleChessPiece rook = null;
        if (move.Castle && _pieces.Remove(move.RookFrom, out var r))
        {
            rook = r;
        }

        Position.Apply(move);
        _pieces[move.To] = mover;
        mover.Piece = Position[move.To]!.Value;
        if (rook != null)
        {
            _pieces[move.RookTo] = rook;
        }

        if (Instant)
        {
            victim?.Vanish();
            mover.SetLocation(Board.ToWorld(move.To), false);
            rook?.SetLocation(Board.ToWorld(move.RookTo), false);
            Finish(move);
            return;
        }

        Animate(move, mover, victim, rook);
    }

    private TimeSpan Step => TimeSpan.FromMilliseconds(S.StepMilliseconds);

    /// <summary>The tiles a piece walks through: straight or diagonal lines square by square; a knight goes along its L.</summary>
    public List<Point3D> PathOf(Square from, Square to, PieceKind kind)
    {
        var path = new List<Point3D>();
        var df = to.File - from.File;
        var dr = to.Rank - from.Rank;
        if (kind == PieceKind.Knight)
        {
            // The long leg first, then the short one.
            var corner = Math.Abs(df) == 2 ? new Square(to.File, from.Rank) : new Square(from.File, to.Rank);
            path.AddRange(PathOf(from, corner, PieceKind.Queen));
            path.AddRange(PathOf(corner, to, PieceKind.Queen));
            return path;
        }

        var n = Math.Max(Math.Abs(df), Math.Abs(dr));
        for (var i = 1; i <= n; i++)
        {
            path.Add(Board.ToWorld(new Square(from.File + Math.Sign(df) * i, from.Rank + Math.Sign(dr) * i)));
        }

        return path;
    }

    private void Animate(ChessMove move, BattleChessPiece mover, BattleChessPiece victim, BattleChessPiece rook)
    {
        var path = PathOf(move.From, move.To, move.Piece.Kind);

        if (victim == null)
        {
            mover.Walk(path, Step, () =>
            {
                if (rook != null)
                {
                    rook.Walk(PathOf(move.RookFrom, move.RookTo, PieceKind.Rook), Step, () => Finish(move));
                }
                else
                {
                    Finish(move);
                }
            });
            return;
        }

        // A capture: walk up to the victim, fight, then step onto its square (en passant: the victim stands beside To).
        var approach = move.EnPassant ? path : path.Take(path.Count - 1).ToList();
        mover.Walk(approach, Step, () =>
            mover.Strike(victim, () =>
            {
                if (move.EnPassant)
                {
                    Finish(move);
                    return;
                }

                mover.Walk([Board.ToWorld(move.To)], Step, () => Finish(move));
            }));
    }

    private void Finish(ChessMove move)
    {
        if (Phase != GamePhase.Moving)
        {
            return;
        }

        // A promoted pawn becomes its new piece where it stands.
        if (move.Promotes && _pieces.TryGetValue(move.To, out var pawn))
        {
            _pieces.Remove(move.To);
            pawn.Vanish();
            var promoted = Spawn(move.To, Position[move.To]!.Value);
            if (!Instant)
            {
                promoted.Say($"*rises as a {ChessRules.Name(move.Promotion)}*");
            }
        }

        foreach (var (sq, p) in _pieces)
        {
            p.Direction = p.Facing;
        }

        Phase = GamePhase.Playing;
        _phaseSince = Clock();
        var who = Opponent(ToMove); // the player who just moved
        var status = Position.Status();
        var king = Position.KingOf(Position.ToMove) is { } ks && _pieces.TryGetValue(ks, out var k) ? k : null;
        var calls = ChessSets.Calls(Set);

        switch (status)
        {
            case ChessStatus.Checkmate:
                king?.PlayAt(calls.Mate);
                king?.Say("*checkmate*");
                End(who, $"{who?.Name} wins by checkmate with {move}.");
                return;
            case ChessStatus.Stalemate:
                End(null, "Stalemate: the player to move has no legal move. The game is drawn.");
                return;
            case ChessStatus.DrawFiftyMoves:
                End(null, "Fifty moves without a capture or a pawn move: the game is drawn.");
                return;
            case ChessStatus.DrawBareKings:
                End(null, "Neither side has enough left to checkmate: the game is drawn.");
                return;
            case ChessStatus.Check:
                king?.PlayAt(calls.Check);
                king?.Say("*check*");
                break;
        }

        SendTurn();
    }

    // ---- the end

    public void Resign(Mobile who)
    {
        if (!IsPlayer(who) || Phase is GamePhase.Over or GamePhase.Inviting)
        {
            if (Phase == GamePhase.Inviting && IsPlayer(who))
            {
                Abort($"{who.Name} called the game off.");
            }

            return;
        }

        End(Opponent(who), $"{who.Name} resigns. {Opponent(who)?.Name} wins.");
    }

    /// <summary>The game ends with a result: the winner (null for a draw) and why. Books it; the pieces leave shortly.</summary>
    public void End(Mobile winner, string reason)
    {
        if (Phase == GamePhase.Over)
        {
            return;
        }

        Phase = GamePhase.Over;
        _phaseSince = Clock();
        Winner = winner;
        Result = reason;
        _pending = null;

        if (winner != null)
        {
            BattleChessRecords.Book(winner, Opponent(winner));
        }
        else
        {
            BattleChessRecords.Book(null, null, White, Black);
        }

        foreach (var m in (Mobile[])[White, Black])
        {
            if (m == null)
            {
                continue;
            }

            if (m.Target is BattleChessTarget)
            {
                m.Target.Cancel(m, TargetCancelType.Canceled);
            }

            m.CloseGump<BattleChessPromotionGump>();
            m.SendMessage(Hue, reason);
            m.SendMessage(Hue, $"Your Battle Chess record: {BattleChessRecords.Of(m)}.");
        }

        SendGumps(reason);

        if (Instant || S.ClearSeconds == 0)
        {
            Clear();
        }
        else
        {
            Timer.DelayCall(TimeSpan.FromSeconds(S.ClearSeconds), Clear);
        }
    }

    /// <summary>Ends the game with no result (staff, a lapsed invitation, a lost piece): nothing is booked.</summary>
    public void Abort(string reason)
    {
        if (Phase == GamePhase.Over && !_games.Contains(this))
        {
            return;
        }

        Phase = GamePhase.Over;
        Result = reason;
        foreach (var m in (Mobile[])[Owner, Guest])
        {
            if (m == null)
            {
                continue;
            }

            if (m.Target is BattleChessTarget or BattleChessInviteTarget)
            {
                m.Target.Cancel(m, TargetCancelType.Canceled);
            }

            m.CloseGump<BattleChessGameGump>();
            m.CloseGump<BattleChessInviteGump>();
            m.CloseGump<BattleChessPromotionGump>();
            if (reason != null)
            {
                m.SendMessage(WarnHue, reason);
            }
        }

        Clear();
    }

    /// <summary>The pieces leave and the board is free again.</summary>
    public void Clear()
    {
        _tick?.Stop();
        _tick = null;
        foreach (var p in _pieces.Values.ToList())
        {
            p.Vanish();
        }

        _pieces.Clear();
        _games.Remove(this);
        if (Board.Game == this)
        {
            Board.Game = null;
        }

        foreach (var m in (Mobile[])[White, Black])
        {
            m?.CloseGump<BattleChessGameGump>();
        }
    }

    /// <summary>A piece deleted from outside the game (staff) ends it without a result.</summary>
    public void OnPieceLost(BattleChessPiece piece)
    {
        if (Phase != GamePhase.Over && _pieces.ContainsValue(piece))
        {
            var sq = _pieces.First(kv => kv.Value == piece).Key;
            _pieces.Remove(sq);
            Abort("A piece was removed from the board, so the game was stopped. No result was kept.");
        }
    }

    // ---- the clock

    /// <summary>Every few seconds: lapsed invitations, a player who does not move, a player who stays away.</summary>
    public void Tick()
    {
        var now = Clock();
        switch (Phase)
        {
            case GamePhase.Inviting:
                if (now - _phaseSince >= TimeSpan.FromMinutes(S.InviteMinutes) || !Usable(Owner))
                {
                    Abort("Nobody answered the invitation, so the board is free again.");
                }

                return;
            case GamePhase.Over:
                return;
        }

        foreach (var m in (Mobile[])[White, Black])
        {
            if (m is not { Deleted: false })
            {
                Abort("A player is gone, so the game was stopped.");
                return;
            }

            if (m.NetState == null)
            {
                if (!_away.TryGetValue(m, out var since))
                {
                    _away[m] = now;
                    Opponent(m)?.SendMessage(WarnHue,
                        $"{m.Name} has left. If they are not back within {S.AwayMinutes} minutes, you win.");
                }
                else if (now - since >= TimeSpan.FromMinutes(S.AwayMinutes))
                {
                    End(Opponent(m), $"{m.Name} left the game and did not come back. {Opponent(m)?.Name} wins.");
                    return;
                }
            }
            else if (_away.Remove(m))
            {
                Opponent(m)?.SendMessage(Hue, $"{m.Name} is back.");
                Resend(m);
            }
        }

        if (Phase is GamePhase.Playing or GamePhase.Promoting &&
            now - _phaseSince >= TimeSpan.FromMinutes(S.MoveMinutes))
        {
            var slow = ToMove;
            End(Opponent(slow), $"{slow?.Name} did not move in {S.MoveMinutes} minutes. {Opponent(slow)?.Name} wins on time.");
        }
    }

    public static void ResetForTests()
    {
        foreach (var g in _games.ToList())
        {
            g.Abort(null);
        }

        _games.Clear();
        InviterIsWhite = Utility.RandomBool;
        Instant = false;
        Clock = () => Core.Now;
    }
}

/// <summary>The cursor for a move: first the piece, then the square it goes to.</summary>
public sealed class BattleChessTarget : Target
{
    private readonly BattleChessGame _game;
    private readonly bool _pick;

    public BattleChessTarget(BattleChessGame game, Mobile m, bool pick) : base(-1, true, TargetFlags.None)
    {
        _game = game;
        _pick = pick;
        CheckLOS = false; // the far squares sit on the dais; a square behind a piece is still a square
    }

    public Square From { get; init; }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (_game.Phase != GamePhase.Playing || from != _game.ToMove)
        {
            return;
        }

        var sq = _game.SquareOf(targeted);
        if (_pick)
        {
            if (_game.Position[sq] is not { } piece || piece.Side != _game.Position.ToMove)
            {
                from.SendMessage(BattleChessGame.WarnHue, sq.Valid ? "Choose one of your own pieces." : "Choose a piece on the board.");
                from.Target = new BattleChessTarget(_game, from, true);
                return;
            }

            from.SendMessage(BattleChessGame.Hue, $"Your {ChessRules.Name(piece.Kind)} on {sq}: choose the square to move it to.");
            from.Target = new BattleChessTarget(_game, from, false) { From = sq };
            return;
        }

        if (!_game.TryPlay(from, From, sq, out var refusal))
        {
            from.SendMessage(BattleChessGame.WarnHue, refusal);
            from.Target = new BattleChessTarget(_game, from, true);
        }
    }

    protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
    {
        if (cancelType == TargetCancelType.Canceled && _game.Phase == GamePhase.Playing && from == _game.ToMove)
        {
            from.SendMessage(BattleChessGame.Hue, "Press Move in the game window, or double-click the board, to choose again.");
        }
    }
}

/// <summary>The cursor for choosing the player to invite.</summary>
public sealed class BattleChessInviteTarget : Target
{
    private readonly BattleChessGame _game;

    public BattleChessInviteTarget(BattleChessGame game) : base(20, false, TargetFlags.None) => _game = game;

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is not Mobile m)
        {
            from.SendMessage(BattleChessGame.WarnHue, "Choose a player.");
            from.Target = new BattleChessInviteTarget(_game);
            return;
        }

        if (!_game.Invite(m, out var refusal))
        {
            from.SendMessage(BattleChessGame.WarnHue, refusal);
            from.Target = new BattleChessInviteTarget(_game);
        }
    }

    protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
    {
        if (cancelType == TargetCancelType.Canceled && _game.Phase == GamePhase.Inviting && _game.Guest == null)
        {
            _game.Abort("You called the game off.");
        }
    }
}
