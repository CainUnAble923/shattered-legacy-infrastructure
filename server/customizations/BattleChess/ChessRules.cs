// ChessRules.cs
//
// cc-P862: the rules of chess for Battle Chess, our own build. The idea of a chess game played by living pieces on a board in
// the world comes from Battle Chess by Arya, kept up by dragon slayer2 (servuo.dev, third-party, reference only). No code was
// taken: this engine is written from the rules of chess, so it knows things the original did not (castling with the king
// moving two squares, en passant on either side, promotion by choice, stalemate, the fifty-move rule and bare kings).
//
// A position is a plain value: no world, no mobiles, no timers. The board in the world (BattleChessGame) asks it whether a
// move is legal, applies it, and asks how the game stands. Files run a..h west to east; rank 1 is White's back rank.

using System;
using System.Collections.Generic;
using System.Text;

namespace Server.BattleChess;

public enum ChessSide
{
    White,
    Black
}

public enum PieceKind
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

public enum ChessStatus
{
    Playing,
    Check,
    Checkmate,
    Stalemate,
    DrawFiftyMoves,
    DrawBareKings
}

public readonly record struct ChessPiece(ChessSide Side, PieceKind Kind)
{
    public override string ToString() => $"{(Side == ChessSide.White ? "White" : "Black")} {ChessRules.Name(Kind)}";
}

public readonly record struct Square(int File, int Rank)
{
    public bool Valid => File is >= 0 and < 8 && Rank is >= 0 and < 8;

    public int Index => Rank * 8 + File;

    public static Square Parse(string s) => new(char.ToLowerInvariant(s[0]) - 'a', s[1] - '1');

    public override string ToString() => Valid ? $"{(char)('a' + File)}{Rank + 1}" : "--";
}

/// <summary>A move with everything it does worked out: what it takes and where, a castle's rook, a promotion.</summary>
public sealed class ChessMove
{
    public Square From { get; init; }
    public Square To { get; init; }
    public ChessPiece Piece { get; init; }

    /// <summary>The piece taken, if any, and the square it stood on (not To for en passant).</summary>
    public ChessPiece? Captured { get; init; }

    public Square CapturedAt { get; init; }
    public bool EnPassant { get; init; }

    /// <summary>A castle: the king goes From to To, and the rook RookFrom to RookTo.</summary>
    public bool Castle { get; init; }

    public Square RookFrom { get; init; }
    public Square RookTo { get; init; }

    /// <summary>A pawn reaching the last rank; Promotion is what it becomes (a queen when nobody chose).</summary>
    public bool Promotes { get; init; }

    public PieceKind Promotion { get; set; } = PieceKind.Queen;

    public override string ToString()
    {
        if (Castle)
        {
            return To.File == 6 ? "O-O" : "O-O-O";
        }

        var sb = new StringBuilder();
        sb.Append(ChessRules.Letter(Piece.Kind)).Append(From).Append(Captured.HasValue ? 'x' : '-').Append(To);
        if (Promotes)
        {
            sb.Append('=').Append(ChessRules.Letter(Promotion));
        }

        return sb.ToString();
    }
}

public static class ChessRules
{
    public static string Name(PieceKind k) => k switch
    {
        PieceKind.Pawn   => "pawn",
        PieceKind.Knight => "knight",
        PieceKind.Bishop => "bishop",
        PieceKind.Rook   => "rook",
        PieceKind.Queen  => "queen",
        _                => "king"
    };

    public static char Letter(PieceKind k) => k switch
    {
        PieceKind.Pawn   => 'P',
        PieceKind.Knight => 'N',
        PieceKind.Bishop => 'B',
        PieceKind.Rook   => 'R',
        PieceKind.Queen  => 'Q',
        _                => 'K'
    };

    /// <summary>The usual material count: pawn 1, knight and bishop 3, rook 5, queen 9.</summary>
    public static int Value(PieceKind k) => k switch
    {
        PieceKind.Pawn   => 1,
        PieceKind.Knight => 3,
        PieceKind.Bishop => 3,
        PieceKind.Rook   => 5,
        PieceKind.Queen  => 9,
        _                => 0
    };

    public static ChessSide Other(ChessSide s) => s == ChessSide.White ? ChessSide.Black : ChessSide.White;

    public static string SideName(ChessSide s) => s == ChessSide.White ? "White" : "Black";

    public static bool IsOver(ChessStatus s) => s is not (ChessStatus.Playing or ChessStatus.Check);
}

public sealed class ChessPosition
{
    private static readonly (int, int)[] KnightSteps = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];
    private static readonly (int, int)[] KingSteps = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];
    private static readonly (int, int)[] Straight = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int, int)[] Diagonal = [(1, 1), (1, -1), (-1, 1), (-1, -1)];

    private readonly ChessPiece?[] _sq = new ChessPiece?[64];

    public ChessSide ToMove { get; private set; } = ChessSide.White;

    // Castling still allowed: the king and that rook have never moved (and the rook was not taken).
    public bool WhiteKingSide { get; private set; }
    public bool WhiteQueenSide { get; private set; }
    public bool BlackKingSide { get; private set; }
    public bool BlackQueenSide { get; private set; }

    /// <summary>The square a pawn just passed over by its double step, where an enemy pawn may take it en passant.</summary>
    public Square? EnPassantSquare { get; private set; }

    /// <summary>Half-moves since a pawn moved or a piece was taken (the fifty-move rule is 100 of these).</summary>
    public int HalfMoves { get; private set; }

    public int FullMoves { get; private set; } = 1;

    public ChessPiece? this[Square s] => s.Valid ? _sq[s.Index] : null;

    public ChessPiece? this[int file, int rank] => this[new Square(file, rank)];

    public static ChessPosition Initial()
    {
        var p = new ChessPosition
        {
            WhiteKingSide = true,
            WhiteQueenSide = true,
            BlackKingSide = true,
            BlackQueenSide = true
        };

        PieceKind[] back = [PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Queen, PieceKind.King, PieceKind.Bishop, PieceKind.Knight, PieceKind.Rook];
        for (var f = 0; f < 8; f++)
        {
            p._sq[new Square(f, 0).Index] = new ChessPiece(ChessSide.White, back[f]);
            p._sq[new Square(f, 1).Index] = new ChessPiece(ChessSide.White, PieceKind.Pawn);
            p._sq[new Square(f, 6).Index] = new ChessPiece(ChessSide.Black, PieceKind.Pawn);
            p._sq[new Square(f, 7).Index] = new ChessPiece(ChessSide.Black, back[f]);
        }

        return p;
    }

    /// <summary>
    /// A position from a short text: pieces as "Ke1 Qd1 pa2 ..." (upper case White, lower case Black, P/p for pawns), then
    /// optional words "black" (Black to move), "K", "Q", "k", "q" (castling rights) and "ep:e3". For the facts and staff tests.
    /// </summary>
    public static ChessPosition FromText(string text)
    {
        var p = new ChessPosition();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (word)
            {
                case "black": p.ToMove = ChessSide.Black; continue;
                case "white": p.ToMove = ChessSide.White; continue;
                case "K": p.WhiteKingSide = true; continue;
                case "Q": p.WhiteQueenSide = true; continue;
                case "k": p.BlackKingSide = true; continue;
                case "q": p.BlackQueenSide = true; continue;
            }

            if (word.StartsWith("ep:", StringComparison.Ordinal))
            {
                p.EnPassantSquare = Square.Parse(word[3..]);
                continue;
            }

            if (word.Length != 3)
            {
                throw new ArgumentException($"Cannot read '{word}' as a piece on a square.");
            }

            var side = char.IsUpper(word[0]) ? ChessSide.White : ChessSide.Black;
            var kind = char.ToUpperInvariant(word[0]) switch
            {
                'P' => PieceKind.Pawn,
                'N' => PieceKind.Knight,
                'B' => PieceKind.Bishop,
                'R' => PieceKind.Rook,
                'Q' => PieceKind.Queen,
                'K' => PieceKind.King,
                _   => throw new ArgumentException($"No piece is called '{word[0]}'.")
            };

            var sq = Square.Parse(word[1..]);
            if (!sq.Valid)
            {
                throw new ArgumentException($"'{word[1..]}' is not a square.");
            }

            p._sq[sq.Index] = new ChessPiece(side, kind);
        }

        return p;
    }

    public ChessPosition Clone()
    {
        var c = new ChessPosition
        {
            ToMove = ToMove,
            WhiteKingSide = WhiteKingSide,
            WhiteQueenSide = WhiteQueenSide,
            BlackKingSide = BlackKingSide,
            BlackQueenSide = BlackQueenSide,
            EnPassantSquare = EnPassantSquare,
            HalfMoves = HalfMoves,
            FullMoves = FullMoves
        };
        Array.Copy(_sq, c._sq, 64);
        return c;
    }

    public IEnumerable<(Square Square, ChessPiece Piece)> Pieces()
    {
        for (var i = 0; i < 64; i++)
        {
            if (_sq[i] is { } piece)
            {
                yield return (new Square(i % 8, i / 8), piece);
            }
        }
    }

    public Square? KingOf(ChessSide side)
    {
        for (var i = 0; i < 64; i++)
        {
            if (_sq[i] is { Kind: PieceKind.King } k && k.Side == side)
            {
                return new Square(i % 8, i / 8);
            }
        }

        return null;
    }

    public static int Forward(ChessSide side) => side == ChessSide.White ? 1 : -1;

    /// <summary>Is the square attacked by any piece of <paramref name="by"/> (as a capture, so pawns only on their diagonals)?</summary>
    public bool IsAttacked(Square s, ChessSide by)
    {
        // Pawns: an enemy pawn attacks from one rank behind it (from the pawn's own forward view).
        var back = -Forward(by);
        foreach (var df in (int[])[-1, 1])
        {
            if (this[s.File + df, s.Rank + back] is { Kind: PieceKind.Pawn } pawn && pawn.Side == by)
            {
                return true;
            }
        }

        foreach (var (df, dr) in KnightSteps)
        {
            if (this[s.File + df, s.Rank + dr] is { Kind: PieceKind.Knight } n && n.Side == by)
            {
                return true;
            }
        }

        foreach (var (df, dr) in KingSteps)
        {
            if (this[s.File + df, s.Rank + dr] is { Kind: PieceKind.King } k && k.Side == by)
            {
                return true;
            }
        }

        return Slides(s, by, Straight, PieceKind.Rook) || Slides(s, by, Diagonal, PieceKind.Bishop);
    }

    private bool Slides(Square s, ChessSide by, (int, int)[] dirs, PieceKind kind)
    {
        foreach (var (df, dr) in dirs)
        {
            var t = new Square(s.File + df, s.Rank + dr);
            while (t.Valid)
            {
                if (this[t] is { } p)
                {
                    if (p.Side == by && (p.Kind == kind || p.Kind == PieceKind.Queen))
                    {
                        return true;
                    }

                    break;
                }

                t = new Square(t.File + df, t.Rank + dr);
            }
        }

        return false;
    }

    public bool InCheck(ChessSide side) => KingOf(side) is { } k && IsAttacked(k, ChessRules.Other(side));

    /// <summary>Every move the side to move could make if its own king's safety did not count.</summary>
    public List<ChessMove> PseudoMoves()
    {
        var list = new List<ChessMove>();
        var side = ToMove;

        foreach (var (from, piece) in Pieces())
        {
            if (piece.Side != side)
            {
                continue;
            }

            switch (piece.Kind)
            {
                case PieceKind.Pawn:
                    PawnMoves(from, piece, list);
                    break;
                case PieceKind.Knight:
                    Steps(from, piece, KnightSteps, list);
                    break;
                case PieceKind.King:
                    Steps(from, piece, KingSteps, list);
                    CastleMoves(from, piece, list);
                    break;
                case PieceKind.Bishop:
                    Rays(from, piece, Diagonal, list);
                    break;
                case PieceKind.Rook:
                    Rays(from, piece, Straight, list);
                    break;
                case PieceKind.Queen:
                    Rays(from, piece, Straight, list);
                    Rays(from, piece, Diagonal, list);
                    break;
            }
        }

        return list;
    }

    private static ChessMove Plain(Square from, Square to, ChessPiece piece, ChessPiece? target) =>
        new() { From = from, To = to, Piece = piece, Captured = target, CapturedAt = to };

    private void Steps(Square from, ChessPiece piece, (int, int)[] steps, List<ChessMove> list)
    {
        foreach (var (df, dr) in steps)
        {
            var to = new Square(from.File + df, from.Rank + dr);
            if (!to.Valid)
            {
                continue;
            }

            var target = this[to];
            if (target == null || target.Value.Side != piece.Side)
            {
                list.Add(Plain(from, to, piece, target));
            }
        }
    }

    private void Rays(Square from, ChessPiece piece, (int, int)[] dirs, List<ChessMove> list)
    {
        foreach (var (df, dr) in dirs)
        {
            var to = new Square(from.File + df, from.Rank + dr);
            while (to.Valid)
            {
                var target = this[to];
                if (target != null)
                {
                    if (target.Value.Side != piece.Side)
                    {
                        list.Add(Plain(from, to, piece, target));
                    }

                    break;
                }

                list.Add(Plain(from, to, piece, null));
                to = new Square(to.File + df, to.Rank + dr);
            }
        }
    }

    private void PawnMoves(Square from, ChessPiece piece, List<ChessMove> list)
    {
        var fw = Forward(piece.Side);
        var last = piece.Side == ChessSide.White ? 7 : 0;
        var start = piece.Side == ChessSide.White ? 1 : 6;

        var one = new Square(from.File, from.Rank + fw);
        if (one.Valid && this[one] == null)
        {
            list.Add(new ChessMove { From = from, To = one, Piece = piece, CapturedAt = one, Promotes = one.Rank == last });

            var two = new Square(from.File, from.Rank + 2 * fw);
            if (from.Rank == start && this[two] == null)
            {
                list.Add(new ChessMove { From = from, To = two, Piece = piece, CapturedAt = two });
            }
        }

        foreach (var df in (int[])[-1, 1])
        {
            var to = new Square(from.File + df, from.Rank + fw);
            if (!to.Valid)
            {
                continue;
            }

            if (this[to] is { } target && target.Side != piece.Side)
            {
                list.Add(new ChessMove
                {
                    From = from, To = to, Piece = piece, Captured = target, CapturedAt = to, Promotes = to.Rank == last
                });
            }
            else if (EnPassantSquare == to)
            {
                var victimAt = new Square(to.File, from.Rank);
                if (this[victimAt] is { Kind: PieceKind.Pawn } victim && victim.Side != piece.Side)
                {
                    list.Add(new ChessMove
                    {
                        From = from, To = to, Piece = piece, Captured = victim, CapturedAt = victimAt, EnPassant = true
                    });
                }
            }
        }
    }

    private void CastleMoves(Square from, ChessPiece king, List<ChessMove> list)
    {
        var rank = king.Side == ChessSide.White ? 0 : 7;
        if (from != new Square(4, rank))
        {
            return;
        }

        var enemy = ChessRules.Other(king.Side);
        var kingSide = king.Side == ChessSide.White ? WhiteKingSide : BlackKingSide;
        var queenSide = king.Side == ChessSide.White ? WhiteQueenSide : BlackQueenSide;

        // The king may not castle out of, through or into check; the squares between king and rook must be empty.
        if ((kingSide || queenSide) && IsAttacked(from, enemy))
        {
            return;
        }

        if (kingSide && this[7, rank] is { Kind: PieceKind.Rook } kr && kr.Side == king.Side &&
            this[5, rank] == null && this[6, rank] == null &&
            !IsAttacked(new Square(5, rank), enemy) && !IsAttacked(new Square(6, rank), enemy))
        {
            list.Add(new ChessMove
            {
                From = from, To = new Square(6, rank), Piece = king, CapturedAt = new Square(6, rank), Castle = true,
                RookFrom = new Square(7, rank), RookTo = new Square(5, rank)
            });
        }

        if (queenSide && this[0, rank] is { Kind: PieceKind.Rook } qr && qr.Side == king.Side &&
            this[1, rank] == null && this[2, rank] == null && this[3, rank] == null &&
            !IsAttacked(new Square(3, rank), enemy) && !IsAttacked(new Square(2, rank), enemy))
        {
            list.Add(new ChessMove
            {
                From = from, To = new Square(2, rank), Piece = king, CapturedAt = new Square(2, rank), Castle = true,
                RookFrom = new Square(0, rank), RookTo = new Square(3, rank)
            });
        }
    }

    /// <summary>The moves the side to move may really make: none leaves its own king attacked.</summary>
    public List<ChessMove> LegalMoves()
    {
        var legal = new List<ChessMove>();
        foreach (var m in PseudoMoves())
        {
            if (!LeavesKingInCheck(m))
            {
                legal.Add(m);
            }
        }

        return legal;
    }

    public bool LeavesKingInCheck(ChessMove m)
    {
        var after = Clone();
        after.Apply(m);
        return after.InCheck(m.Piece.Side);
    }

    /// <summary>Plays the move (assumed legal) and hands the turn over.</summary>
    public void Apply(ChessMove m)
    {
        if (m.Captured.HasValue)
        {
            _sq[m.CapturedAt.Index] = null;
        }

        _sq[m.From.Index] = null;
        _sq[m.To.Index] = m.Promotes ? new ChessPiece(m.Piece.Side, m.Promotion) : m.Piece;

        if (m.Castle)
        {
            _sq[m.RookTo.Index] = _sq[m.RookFrom.Index];
            _sq[m.RookFrom.Index] = null;
        }

        // Castling rights: a king that moves loses both; a rook that moves, or is taken on its corner, loses its side.
        if (m.Piece.Kind == PieceKind.King)
        {
            if (m.Piece.Side == ChessSide.White)
            {
                WhiteKingSide = WhiteQueenSide = false;
            }
            else
            {
                BlackKingSide = BlackQueenSide = false;
            }
        }

        LoseRightsAt(m.From);
        LoseRightsAt(m.CapturedAt);

        EnPassantSquare = m.Piece.Kind == PieceKind.Pawn && Math.Abs(m.To.Rank - m.From.Rank) == 2
            ? new Square(m.From.File, (m.From.Rank + m.To.Rank) / 2)
            : null;

        HalfMoves = m.Piece.Kind == PieceKind.Pawn || m.Captured.HasValue ? 0 : HalfMoves + 1;
        if (ToMove == ChessSide.Black)
        {
            FullMoves++;
        }

        ToMove = ChessRules.Other(ToMove);
    }

    private void LoseRightsAt(Square s)
    {
        if (s == new Square(0, 0)) { WhiteQueenSide = false; }
        else if (s == new Square(7, 0)) { WhiteKingSide = false; }
        else if (s == new Square(0, 7)) { BlackQueenSide = false; }
        else if (s == new Square(7, 7)) { BlackKingSide = false; }
    }

    /// <summary>How the game stands for the side to move.</summary>
    public ChessStatus Status()
    {
        var check = InCheck(ToMove);
        if (LegalMoves().Count == 0)
        {
            return check ? ChessStatus.Checkmate : ChessStatus.Stalemate;
        }

        if (HalfMoves >= 100)
        {
            return ChessStatus.DrawFiftyMoves;
        }

        if (BareKings())
        {
            return ChessStatus.DrawBareKings;
        }

        return check ? ChessStatus.Check : ChessStatus.Playing;
    }

    /// <summary>Neither side can ever mate: kings alone, or kings and one knight or bishop between them.</summary>
    public bool BareKings()
    {
        var minors = 0;
        foreach (var (_, p) in Pieces())
        {
            switch (p.Kind)
            {
                case PieceKind.King:
                    continue;
                case PieceKind.Knight or PieceKind.Bishop:
                    minors++;
                    break;
                default:
                    return false;
            }
        }

        return minors <= 1;
    }

    /// <summary>
    /// Finds the legal move from one square to another for the side to move, or says plainly why there is none. A king
    /// moved onto its own rook, or two squares toward it, is a castle.
    /// </summary>
    public ChessMove FindMove(Square from, Square to, out string refusal)
    {
        refusal = null;
        if (!from.Valid || !to.Valid)
        {
            refusal = "That is not a square of the board.";
            return null;
        }

        if (this[from] is not { } piece)
        {
            refusal = "There is no piece there.";
            return null;
        }

        if (piece.Side != ToMove)
        {
            refusal = $"That is a {ChessRules.SideName(piece.Side).ToLowerInvariant()} piece; it is {ChessRules.SideName(ToMove)}'s move.";
            return null;
        }

        if (from == to)
        {
            refusal = "Choose a different square to move to.";
            return null;
        }

        // The king onto its own rook means a castle on that side.
        if (piece.Kind == PieceKind.King && this[to] is { Kind: PieceKind.Rook } own && own.Side == piece.Side && to.Rank == from.Rank)
        {
            to = new Square(to.File > from.File ? 6 : 2, from.Rank);
        }

        ChessMove pseudo = null;
        foreach (var m in PseudoMoves())
        {
            if (m.From == from && m.To == to)
            {
                pseudo = m;
                break;
            }
        }

        if (pseudo == null)
        {
            refusal = WhyNot(piece, from, to);
            return null;
        }

        if (LeavesKingInCheck(pseudo))
        {
            refusal = InCheck(piece.Side)
                ? "Your king is in check: that move does not get it out."
                : "That move would leave your king in check.";
            return null;
        }

        return pseudo;
    }

    private string WhyNot(ChessPiece piece, Square from, Square to)
    {
        if (this[to] is { } there && there.Side == piece.Side)
        {
            return "You can't take your own piece.";
        }

        var df = to.File - from.File;
        var dr = to.Rank - from.Rank;
        return piece.Kind switch
        {
            PieceKind.Pawn when dr * Forward(piece.Side) <= 0 => "Pawns only move forward.",
            PieceKind.Pawn when df != 0 => "A pawn takes one square diagonally forward, and only when there is a piece to take.",
            PieceKind.Pawn => "A pawn moves one square forward (two from its first square), and only onto an empty square.",
            PieceKind.Knight => "A knight moves in an L: two squares one way and one square to the side.",
            PieceKind.Bishop when Math.Abs(df) != Math.Abs(dr) => "A bishop moves diagonally.",
            PieceKind.Rook when df != 0 && dr != 0 => "A rook moves in straight lines.",
            PieceKind.Queen when df != 0 && dr != 0 && Math.Abs(df) != Math.Abs(dr) => "A queen moves in straight lines or diagonally.",
            PieceKind.King when Math.Abs(df) == 2 && dr == 0 =>
                "You can't castle now: the king and that rook must not have moved, the squares between must be empty, and the king may not be in, pass through or land in check.",
            PieceKind.King => "A king moves one square at a time.",
            _ => "Another piece is in the way."
        };
    }

    /// <summary>The board as text, rank 8 at the top (for the facts and for staff).</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var r = 7; r >= 0; r--)
        {
            for (var f = 0; f < 8; f++)
            {
                var p = this[f, r];
                var c = p == null ? '.' : ChessRules.Letter(p.Value.Kind);
                sb.Append(p is { Side: ChessSide.Black } ? char.ToLowerInvariant(c) : c);
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
}
