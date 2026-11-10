// BattleChessPiece.cs
//
// cc-P862: a living chess piece. A plain mobile (no AI, no skills, no loot, blessed) that stands on its square, walks to
// another square one tile at a time when its player moves it, and fights the piece it takes. It belongs to a running game
// only: a save keeps it, and the next start deletes it (a game does not survive a restart).
//
// The five sets (Classic, Fantasy, Giants, Animals, Undead) take their bodies and sounds from Battle Chess by Arya, kept up
// by dragon slayer2 (third-party, reference only); the code is ours.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.BattleChess;

public enum ChessSet
{
    Classic,
    Fantasy,
    Giants,
    Animals,
    Undead
}

/// <summary>What a piece of one set looks and sounds like.</summary>
public sealed record PieceLook(int Body, int Move, int Capture, int Death, bool Dressed = false, bool Female = false);

public static class ChessSets
{
    public const int WhiteAccent = 96;
    public const int BlackAccent = 447;

    // [set][kind]: kinds in PieceKind order (Pawn, Knight, Bishop, Rook, Queen, King).
    private static readonly PieceLook[][] Looks =
    [
        // Classic: people in the side's colours; the rook is an earth elemental.
        [
            new(0x190, 821, 1094, 1059, true), new(0x190, 588, 168, 170, true), new(0x190, 251, 773, 1063, true),
            new(14, 287, 268, 269), new(0x191, 823, 824, 814, true, true), new(0x190, 1055, 1068, 0, true)
        ],
        // Fantasy: horde minion, centaur, evil mage, troll, succubus, betrayer.
        [
            new(776, 408, 927, 411), new(101, 762, 758, 759), new(124, 579, 283, 250),
            new(55, 461, 463, 465), new(149, 1200, 1201, 1202), new(767, 610, 606, 0)
        ],
        // Giants: devourer, flesh renderer, wanderer of the void, abysmal horror, semidar, shadow knight.
        [
            new(303, 709, 712, 705), new(315, 875, 378, 879), new(316, 373, 372, 376),
            new(312, 767, 367, 371), new(174, 883, 880, 888), new(311, 1152, 1150, 0)
        ],
        // Animals: walrus, pack llama, giant toad, gorilla, cow, polar bear.
        [
            new(221, 1217, 226, 228), new(292, 183, 1011, 185), new(80, 858, 616, 623),
            new(29, 159, 158, 162), new(216, 123, 120, 124), new(213, 95, 1223, 98)
        ],
        // Undead: skeleton, skeletal knight, lich, mummy, banshee, skeletal mage.
        [
            new(56, 451, 1169, 455), new(147, 588, 1164, 416), new(78, 415, 1004, 1005),
            new(154, 880, 357, 1156), new(310, 896, 382, 1202), new(148, 1163, 1162, 361)
        ]
    ];

    // The king's check and checkmate sounds per set.
    private static readonly (int Check, int Mate)[] KingCalls = [(1086, 1088), (604, 613), (1152, 1149), (98, 99), (772, 716)];

    public static PieceLook Look(ChessSet set, PieceKind kind) => Looks[(int)set][(int)kind];

    public static (int Check, int Mate) Calls(ChessSet set) => KingCalls[(int)set];

    public static string Describe(ChessSet set) => set switch
    {
        ChessSet.Classic => "Classic: soldiers, knights, priests, earth elementals, a queen, a king",
        ChessSet.Fantasy => "Fantasy: horde minions, centaurs, mages, trolls, a succubus, a betrayer",
        ChessSet.Giants  => "Giants: devourers, flesh renderers, void wanderers, abysmal horrors",
        ChessSet.Animals => "Animals: walruses, llamas, toads, gorillas, a cow, a polar bear",
        _                => "Undead: skeletons, skeletal knights, liches, mummies, a banshee, a mage"
    };

    /// <summary>The clothes and gear of the Classic set: (item ID, layer, accent hue instead of the side's).</summary>
    public static (int ItemID, Layer Layer, bool Accent)[] Dress(PieceKind kind) => kind switch
    {
        PieceKind.Pawn =>
        [
            (0x13BF, Layer.InnerTorso, false), (0x13BE, Layer.Pants, true), (0x170B, Layer.Shoes, false),
            (0x1B73, Layer.TwoHanded, true), (0x13B6, Layer.OneHanded, true)
        ],
        PieceKind.Knight =>
        [
            (0x1415, Layer.InnerTorso, false), (0x1411, Layer.Pants, false), (0x1410, Layer.Arms, false),
            (0x1413, Layer.Neck, false), (0x1414, Layer.Gloves, false), (0x1412, Layer.Helm, false),
            (0x1F7B, Layer.MiddleTorso, true), (0x0F62, Layer.TwoHanded, true)
        ],
        PieceKind.Bishop =>
        [
            (0x2684, Layer.OuterTorso, false), (0x170B, Layer.Shoes, true), (0x0E89, Layer.TwoHanded, true)
        ],
        PieceKind.Queen =>
        [
            (0x1EFF, Layer.InnerTorso, false), (0x170D, Layer.Shoes, true), (0x26BC, Layer.OneHanded, true)
        ],
        PieceKind.King =>
        [
            (0x1539, Layer.Pants, false), (0x1EFD, Layer.Shirt, false), (0x1F7B, Layer.MiddleTorso, true),
            (0x1515, Layer.Cloak, true), (0x170B, Layer.Shoes, true), (0x26BC, Layer.OneHanded, true)
        ],
        _ => []
    };
}

[SerializationGenerator(0, false)]
public partial class BattleChessPiece : Mobile
{
    private Timer _walk;

    public BattleChessPiece(BattleChessGame game, ChessPiece piece, ChessSet set, int sideHue)
    {
        Game = game;
        Piece = piece;
        Set = set;
        var look = ChessSets.Look(set, piece.Kind);
        Look = look;

        Name = $"{(piece.Side == ChessSide.White ? "a white" : "a black")} {ChessRules.Name(piece.Kind)}";
        Body = look.Body;
        Female = look.Female;
        Blessed = true;
        Player = false;
        CantWalk = true;

        if (look.Dressed)
        {
            Hue = Race.Human.RandomSkinHue();
            HairItemID = look.Female ? 0x203C : piece.Kind == PieceKind.Knight ? 0x203D : 0x203B;
            HairHue = Race.Human.RandomHairHue();
            var accent = piece.Side == ChessSide.White ? ChessSets.WhiteAccent : ChessSets.BlackAccent;
            foreach (var (id, layer, useAccent) in ChessSets.Dress(piece.Kind))
            {
                AddItem(new Item(id) { Layer = layer, Hue = useAccent ? accent : sideHue, Movable = false });
            }
        }
        else
        {
            Hue = sideHue;
        }
    }

    public BattleChessGame Game { get; private set; }
    public ChessPiece Piece { get; set; }
    public ChessSet Set { get; private set; }
    public PieceLook Look { get; private set; }

    /// <summary>The side's own facing: White looks north up the board, Black south.</summary>
    public Direction Facing => Piece.Side == ChessSide.White ? Direction.North : Direction.South;

    public bool Walking => _walk?.Running == true;

    public override bool CanPaperdollBeOpenedBy(Mobile from) => false;

    public override void OnDoubleClick(Mobile from) => Game?.OnPieceClicked(from, this);

    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) => false;

    public override bool OnMoveOver(Mobile m) => true;

    public override bool HandlesOnSpeech(Mobile from) => false;

    public void PlayAt(int sound)
    {
        if (sound > 0 && Map != null)
        {
            Effects.PlaySound(Location, Map, sound);
        }
    }

    /// <summary>The direction to step from one tile to the next (neighbours only).</summary>
    public static Direction Toward(Point3D from, Point3D to)
    {
        var dx = Math.Sign(to.X - from.X);
        var dy = Math.Sign(to.Y - from.Y);
        return (dx, dy) switch
        {
            (0, -1)  => Direction.North,
            (1, -1)  => Direction.Right,
            (1, 0)   => Direction.East,
            (1, 1)   => Direction.Down,
            (0, 1)   => Direction.South,
            (-1, 1)  => Direction.Left,
            (-1, 0)  => Direction.West,
            (-1, -1) => Direction.Up,
            _        => Direction.North
        };
    }

    /// <summary>Walks the tiles in order, one per step, then calls <paramref name="arrived"/>.</summary>
    public void Walk(IReadOnlyList<Point3D> path, TimeSpan step, Action arrived)
    {
        _walk?.Stop();
        PlayAt(Look.Move);

        var i = 0;
        if (path.Count == 0)
        {
            arrived?.Invoke();
            return;
        }

        _walk = Timer.DelayCall(step, step, path.Count, () =>
        {
            if (Deleted || Map == null)
            {
                _walk?.Stop();
                return;
            }

            var next = path[i++];
            Direction = Toward(Location, next) | Direction.Running;
            SetLocation(next, false);

            if (i >= path.Count)
            {
                Direction = Facing;
                arrived?.Invoke();
            }
        });
    }

    /// <summary>The fight on a capture: this piece strikes, the victim staggers, lightning falls, the victim is gone.</summary>
    public void Strike(BattleChessPiece victim, Action done)
    {
        Direction = Toward(Location, victim.Location);
        victim.Direction = Toward(victim.Location, Location);
        PlayAt(Look.Capture);
        Animate(Body.IsHuman ? 9 : 4, 7, 1, true, false, 0);

        Timer.DelayCall(TimeSpan.FromMilliseconds(700), () =>
        {
            if (!victim.Deleted)
            {
                victim.Animate(victim.Body.IsHuman ? 20 : 10, 5, 1, true, false, 0);
                victim.FixedParticles(0x3779, 10, 25, 5002, EffectLayer.Waist);
            }
        });

        Timer.DelayCall(TimeSpan.FromMilliseconds(1300), () =>
        {
            if (!victim.Deleted)
            {
                victim.BoltEffect(0);
                victim.PlayAt(victim.Look.Death);
                victim.Game = null; // gone from the game; its deletion is not a staff deletion
                victim.Delete();
            }

            done?.Invoke();
        });
    }

    /// <summary>Leaves the board quietly (the game is over or was ended).</summary>
    public void Vanish()
    {
        _walk?.Stop();
        Game = null;
        if (Map != null)
        {
            Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
        }

        Delete();
    }

    public override void OnAfterDelete()
    {
        _walk?.Stop();
        base.OnAfterDelete();

        // A piece deleted by staff in the middle of a game ends that game.
        Game?.OnPieceLost(this);
        Game = null;
    }

    [AfterDeserialization]
    private void AfterDeserialization() => Timer.DelayCall(Delete);
}
