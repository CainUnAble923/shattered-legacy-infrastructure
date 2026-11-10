// BattleChessRecords.cs
//
// cc-P862: each player's Battle Chess record (wins, losses, draws), kept for pride only: no gold, no items, no points of any
// kind. The original handed the winner a "winner paper"; we keep the score instead and show it in the board's window.
// Saved through pinned's GenericPersistence ("BattleChess"), versioned; an unknown version fails loudly.

using System;
using System.Collections.Generic;
using System.IO;

namespace Server.BattleChess;

public sealed class ChessRecord
{
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }

    public int Games => Wins + Losses + Draws;

    public override string ToString() => $"{Wins} won, {Losses} lost, {Draws} drawn";
}

public class BattleChessRecords : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static BattleChessRecords _instance;

    public static readonly Dictionary<Serial, ChessRecord> Records = new();

    public BattleChessRecords() : base("BattleChess", 10)
    {
    }

    public static void Configure() => _instance ??= new BattleChessRecords();

    public static ChessRecord Of(Mobile m) =>
        m != null && Records.TryGetValue(m.Serial, out var r) ? r : new ChessRecord();

    private static ChessRecord Edit(Mobile m)
    {
        if (!Records.TryGetValue(m.Serial, out var r))
        {
            Records[m.Serial] = r = new ChessRecord();
        }

        return r;
    }

    /// <summary>Books a finished game: a winner and a loser, or (winner null) a draw between the two.</summary>
    public static void Book(Mobile winner, Mobile loser, Mobile a = null, Mobile b = null)
    {
        if (winner != null && loser != null)
        {
            Edit(winner).Wins++;
            Edit(loser).Losses++;
            return;
        }

        if (a != null && b != null)
        {
            Edit(a).Draws++;
            Edit(b).Draws++;
        }
    }

    public static void ResetForTests() => Records.Clear();

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(Records.Count);
        foreach (var (serial, r) in Records)
        {
            writer.Write(serial);
            writer.WriteEncodedInt(r.Wins);
            writer.WriteEncodedInt(r.Losses);
            writer.WriteEncodedInt(r.Draws);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"BattleChessRecords version {version} is not one this build can read.");
        }

        Records.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            Records[serial] = new ChessRecord
            {
                Wins = reader.ReadEncodedInt(),
                Losses = reader.ReadEncodedInt(),
                Draws = reader.ReadEncodedInt()
            };
        }
    }
}
