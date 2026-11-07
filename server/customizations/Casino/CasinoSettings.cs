// CasinoSettings.cs
//
// cc-P811 (P797 rank 2): every number of the small casino. Configuration/casino.json, written with these defaults on first
// start and read at start; a bad file is refused whole, every error is printed, and the defaults stay in force. [Casino reload
// reads it again. Notes: shard-migration notes/cc-P811-casino.md.
//
// The slot machine's payback is not a number typed in: it is worked out exactly from the reel weights and the pay table
// (SlotPayback), and a table that would pay back MaxPaybackPercent or more is refused. The blackjack table's edge comes from
// its rules (6:5 blackjack, the dealer hits soft 17, no splitting), measured by simulation in the facts.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Server.Casino;

public sealed class SlotSymbol
{
    public string Name { get; set; } = "";
    public int Weight { get; set; } = 1;   // on every reel
    public int Hue { get; set; }           // the label's colour in the machine's window
    public int PaysThree { get; set; }     // three in a row pay this many times the bet
}

public sealed class CasinoPlacement
{
    public string Kind { get; set; } = "Slot";  // Slot or Blackjack
    public string Map { get; set; } = "Trammel";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public string Where { get; set; } = "the Haven inn";
}

public sealed class CasinoSettings
{
    public const string FileName = "casino.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static CasinoSettings Current { get; set; } = new();

    public bool Enabled { get; set; } = true;
    public bool AllowYoung { get; set; }
    public int UseRange { get; set; } = 2;
    public int DailyWagerLimit { get; set; } = 250000;   // per account, both games together
    public double MaxPaybackPercent { get; set; } = 99;  // a slot table paying back this much or more is refused
    public int NewsAtMultiplier { get; set; } = 100;     // a slot win this many times the bet or more is told to the shard
    public int NewsAtGold { get; set; } = 25000;         // and so is any win (either game) of this much gold or more

    // ---- the slot machine
    public int SlotItemID { get; set; } = 0x9A96;        // the brass contraption the client calls "machine coffee"
    public int SlotHue { get; set; } = 0x8A5;
    public List<int> SlotBets { get; set; } = [10, 100, 1000, 5000];
    public int PaysTwoCherries { get; set; } = 1;        // exactly two cherries (any reels) give the bet back

    public List<SlotSymbol> Symbols { get; set; } =
    [
        new() { Name = "Cherry", Weight = 8, Hue = 0x21, PaysThree = 8 },
        new() { Name = "Lemon", Weight = 7, Hue = 0x35, PaysThree = 10 },
        new() { Name = "Bell", Weight = 5, Hue = 0x2C, PaysThree = 25 },
        new() { Name = "Bar", Weight = 4, Hue = 0x3B2, PaysThree = 40 },
        new() { Name = "Seven", Weight = 2, Hue = 0x26, PaysThree = 100 },
        new() { Name = "Gem", Weight = 1, Hue = 0x59, PaysThree = 1000 },
    ];

    // ---- blackjack
    public int TableItemID { get; set; } = 0xB34;
    public List<int> BlackjackBets { get; set; } = [100, 500, 1000, 5000];
    public int BlackjackPaysNumerator { get; set; } = 6;   // a natural blackjack pays 6:5
    public int BlackjackPaysDenominator { get; set; } = 5;
    public bool DealerHitsSoft17 { get; set; } = true;
    public bool AllowDouble { get; set; } = true;

    public List<CasinoPlacement> Placements { get; set; } =
    [
        new() { Kind = "Slot", X = 3670, Y = 2608, Z = 0 },
        new() { Kind = "Slot", X = 3672, Y = 2608, Z = 0 },
        new() { Kind = "Blackjack", X = 3671, Y = 2610, Z = 0 },
    ];

    /// <summary>The slot machine's exact payback, in percent of what is played: every reel stop weighed, every win paid.</summary>
    public double SlotPayback()
    {
        double total = Symbols.Sum(s => s.Weight);
        if (total <= 0)
        {
            return 0;
        }

        var back = 0.0;
        foreach (var s in Symbols)
        {
            var p = s.Weight / total;
            back += p * p * p * s.PaysThree;
        }

        var cherry = Symbols.FirstOrDefault(s => s.Name.Equals("Cherry", StringComparison.OrdinalIgnoreCase));
        if (cherry != null)
        {
            var c = cherry.Weight / total;
            back += 3 * c * c * (1 - c) * PaysTwoCherries;
        }

        return back * 100;
    }

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static string ToJson(CasinoSettings s) => JsonSerializer.Serialize(s, _json) + "\n";

    private static void Range(double value, double min, double max, string name, List<string> errors)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            errors.Add($"{name} must be from {min} to {max} (it is {value}).");
        }
    }

    public static CasinoSettings Parse(string json, List<string> errors)
    {
        CasinoSettings s;

        try
        {
            s = JsonSerializer.Deserialize<CasinoSettings>(json, _json);
        }
        catch (JsonException ex)
        {
            errors.Add($"not valid JSON: {ex.Message}");
            return null;
        }

        if (s == null)
        {
            errors.Add("the file is empty.");
            return null;
        }

        Range(s.UseRange, 1, 10, nameof(UseRange), errors);
        Range(s.DailyWagerLimit, 0, 100_000_000, nameof(DailyWagerLimit), errors);
        Range(s.MaxPaybackPercent, 50, 99.9, nameof(MaxPaybackPercent), errors);
        Range(s.NewsAtMultiplier, 1, 100000, nameof(NewsAtMultiplier), errors);
        Range(s.NewsAtGold, 1, 100_000_000, nameof(NewsAtGold), errors);
        Range(s.PaysTwoCherries, 0, 100, nameof(PaysTwoCherries), errors);
        Range(s.BlackjackPaysNumerator, 1, 10, nameof(BlackjackPaysNumerator), errors);
        Range(s.BlackjackPaysDenominator, 1, 10, nameof(BlackjackPaysDenominator), errors);

        if (s.BlackjackPaysNumerator > s.BlackjackPaysDenominator * 3 / 2)
        {
            errors.Add("A natural blackjack may pay at most 3:2.");
        }

        foreach (var (bets, name) in new[] { (s.SlotBets, nameof(SlotBets)), (s.BlackjackBets, nameof(BlackjackBets)) })
        {
            if (bets is not { Count: >= 1 and <= 6 } || bets.Any(b => b is < 1 or > 1_000_000))
            {
                errors.Add($"{name}: one to six bets, each 1 to 1,000,000 gold.");
            }
        }

        if (s.Symbols is not { Count: >= 2 } || s.Symbols.Any(x => x.Weight < 1 || x.PaysThree < 0 || string.IsNullOrWhiteSpace(x.Name)))
        {
            errors.Add("Symbols: at least two, each with a name, a weight of 1 or more and a pay of 0 or more.");
        }
        else if (s.SlotPayback() >= s.MaxPaybackPercent)
        {
            errors.Add($"The slot table pays back {s.SlotPayback():0.00}%, not under MaxPaybackPercent ({s.MaxPaybackPercent}%).");
        }

        foreach (var p in s.Placements ?? [])
        {
            if (p.Kind is not ("Slot" or "Blackjack"))
            {
                errors.Add($"placement {p.X},{p.Y}: Kind is Slot or Blackjack.");
            }

            if (Server.Map.Parse(p.Map ?? "") == null)
            {
                errors.Add($"placement {p.X},{p.Y}: no facet \"{p.Map}\".");
            }
        }

        return errors.Count == 0 ? s : null;
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        if (File.Exists(path))
        {
            return Reload(path);
        }

        Current = new CasinoSettings();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToJson(Current));
            return [$"{FileName} was missing; wrote the built-in defaults to {path}."];
        }
        catch (Exception ex)
        {
            return [$"{FileName} is missing and could not be written ({ex.Message}); using the built-in defaults."];
        }
    }

    public static List<string> Reload(string path)
    {
        var errors = new List<string>();
        CasinoSettings s = null;

        try
        {
            s = Parse(File.ReadAllText(path), errors);
        }
        catch (Exception ex)
        {
            errors.Add($"could not read it: {ex.Message}");
        }

        if (s == null)
        {
            errors.Insert(0, $"{FileName} REFUSED, the settings in force are kept:");
            return errors;
        }

        Current = s;
        return [$"{FileName} loaded: the slot table pays back {s.SlotPayback():0.00}%."];
    }
}
