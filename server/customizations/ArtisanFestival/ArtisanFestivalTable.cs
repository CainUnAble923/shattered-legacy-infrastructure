// cc-P783: the Artisan Festival pieces as data: Configuration/artisan-festival.json, written with the defaults on first
// start if missing, read at start and by [ArtisanFestivalStaff reload. A bad file is refused whole, every error is
// printed, and the table in force is kept (the RequisitionTable pattern). Notes in shard-migration
// notes/cc-P783-artisan-festival-decor.md.
//
// The 64 pieces are OSI's Artisan Festival rewards (2016-2019; Publish 101 art in the client: lanterns, wooden
// sculptures, throw pillows and tapestries for the virtues and the dungeons), the idea and the item IDs from Sareus's
// script pack (servuo.dev). Each piece: a key, the name shown, its kind, its virtue or dungeon, and its facings. A facing
// is an item ID and, for a light, the lit ID; a piece with two facings turns between them (double-click it in the pack).
// Lanterns: two facings, lit and unlit. Sculptures: one facing, lit and unlit. Pillows and tapestries: two facings.
//
//   stock       the pieces the Festival Peddler sells in spring, and their Festival Token prices (reward-stores.json
//               can still override a price by the row's key, festival.spring.artisan.<key>, as for every row)
//   boxWeight   every piece not in the stock goes in the spring loot box slot, at this weight each (the slot's own
//               entries in loot-boxes.json keep theirs)
//   light       the light a lit piece gives (a LightType name)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Engines.ArtisanFestival;

public enum ArtisanKind
{
    Lantern,
    Sculpture,
    Pillow,
    Tapestry
}

public sealed class ArtisanFacing
{
    public int Id { get; set; }
    public int Lit { get; set; }
}

public sealed class ArtisanPiece
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public ArtisanKind Kind { get; set; }
    public string Virtue { get; set; } = "";
    public List<ArtisanFacing> Facings { get; set; } = [];

    [JsonIgnore]
    public bool IsLight => Kind is ArtisanKind.Lantern or ArtisanKind.Sculpture;
}

public sealed class ArtisanStockRow
{
    public string Key { get; set; } = "";
    public int Price { get; set; }
}

public sealed class ArtisanFestivalTable
{
    public const string FileName = "artisan-festival.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public string About { get; set; }
    public string Light { get; set; } = "Circle300";
    public int BoxWeight { get; set; } = 2;
    public List<ArtisanStockRow> Stock { get; set; } = [];
    public List<ArtisanPiece> Pieces { get; set; } = [];

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    [JsonIgnore]
    public LightType LightType => Enum.TryParse<LightType>(Light, true, out var l) ? l : LightType.Circle300;

    private static ArtisanFestivalTable _defaults;
    private static ArtisanFestivalTable _current;

    public static ArtisanFestivalTable Defaults => _defaults ??= BuildDefaults();

    public static ArtisanFestivalTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    public ArtisanPiece Piece(string key) =>
        Pieces.FirstOrDefault(p => p.Key.Equals(key ?? "", StringComparison.OrdinalIgnoreCase));

    public bool InStock(string key) => Stock.Any(s => s.Key.Equals(key ?? "", StringComparison.OrdinalIgnoreCase));

    /// <summary>The pieces the spring loot box slot may give: every piece not in the stock.</summary>
    public IEnumerable<ArtisanPiece> BoxPieces => Pieces.Where(p => !InStock(p.Key));

    // -- Defaults -----------------------------------------------------------------------------------

    private static ArtisanPiece P(string key, string name, ArtisanKind kind, string virtue, params (int Id, int Lit)[] facings) =>
        new() { Key = key, Name = name, Kind = kind, Virtue = virtue, Facings = facings.Select(f => new ArtisanFacing { Id = f.Id, Lit = f.Lit }).ToList() };

    private static ArtisanStockRow S(string key, int price) => new() { Key = key, Price = price };

    public static ArtisanFestivalTable BuildDefaults() => new()
    {
        About = "Artisan Festival decor (cc-P783): OSI's 2016-2019 Artisan Festival rewards, after Sareus's script pack. " +
                "Edit and run [ArtisanFestivalStaff reload; a bad file is refused whole and the table in force is kept. " +
                "pieces: key, name, kind (Lantern, Sculpture, Pillow, Tapestry), virtue, facings (id, and lit for a light). " +
                "stock: the spring Festival Peddler's rows and their token prices. boxWeight: each other piece's weight in " +
                "the spring loot box slot.",
        Light = "Circle300",
        BoxWeight = 2,
        // Twelve for the spring stall: four lanterns, four pillows, two tapestries, two sculptures, one per virtue
        // where it can be (a lantern 15 tokens, a pillow 10, a tapestry 15, a sculpture 25).
        Stock =
        [
            S("honor-lantern", 15), S("compassion-lantern", 15), S("valor-lantern", 15), S("spirituality-lantern", 15),
            S("honesty-pillow", 10), S("justice-pillow", 10), S("sacrifice-pillow", 10), S("humility-pillow", 10),
            S("covetous-tapestry", 15), S("hythloth-tapestry", 15),
            S("deceit-sculpture", 25), S("pride-sculpture", 25),
        ],
        // The IDs are Sareus's, checked present in our client's art (notes, cc-P783-tools/art-check.txt).
        Pieces =
        [
            P("sacrifice-lantern", "a paper tear drop lantern", ArtisanKind.Lantern, "Sacrifice", (40423, 40449), (40460, 40465)),
            P("destard-lantern", "a paper skull lantern", ArtisanKind.Lantern, "Destard", (40424, 40455), (40461, 40471)),
            P("hythloth-lantern", "a paper crescent moon lantern", ArtisanKind.Lantern, "Hythloth", (40422, 40452), (40459, 40468)),
            P("honor-lantern", "a paper chalice lantern", ArtisanKind.Lantern, "Honor", (40421, 40446), (40458, 40462)),
            P("covetous-lantern", "a paper coin lantern", ArtisanKind.Lantern, "Covetous", (41076, 41077), (41080, 41081)),
            P("pride-lantern", "a paper sceptre lantern", ArtisanKind.Lantern, "Pride", (41084, 41085), (41088, 41089)),
            P("justice-lantern", "a paper balanced scales lantern", ArtisanKind.Lantern, "Justice", (41092, 41093), (41096, 41097)),
            P("humility-lantern", "a paper crooks lantern", ArtisanKind.Lantern, "Humility", (41100, 41101), (41104, 41105)),
            P("spirituality-lantern", "a paper ankh lantern", ArtisanKind.Lantern, "Spirituality", (41506, 41507), (41510, 41511)),
            P("despise-lantern", "a paper axe lantern", ArtisanKind.Lantern, "Despise", (41514, 41515), (41518, 41519)),
            P("valor-lantern", "a paper sword lantern", ArtisanKind.Lantern, "Valor", (41522, 41523), (41526, 41527)),
            P("deceit-lantern", "a paper masks lantern", ArtisanKind.Lantern, "Deceit", (41530, 41531), (41534, 41535)),
            P("shame-lantern", "a paper broken shield lantern", ArtisanKind.Lantern, "Shame", (42089, 42090), (42093, 42094)),
            P("honesty-lantern", "a paper hand lantern", ArtisanKind.Lantern, "Honesty", (42097, 42098), (42101, 42102)),
            P("wrong-lantern", "a paper broken scales lantern", ArtisanKind.Lantern, "Wrong", (42105, 42106), (42109, 42110)),
            P("compassion-lantern", "a paper heart lantern", ArtisanKind.Lantern, "Compassion", (42113, 42114), (42117, 42118)),
            P("valor-sculpture", "a wooden Valor sculpture", ArtisanKind.Sculpture, "Valor", (40576, 40430)),
            P("despise-sculpture", "a wooden Despise sculpture", ArtisanKind.Sculpture, "Despise", (40577, 40434)),
            P("spirituality-sculpture", "a wooden Spirituality sculpture", ArtisanKind.Sculpture, "Spirituality", (40578, 40438)),
            P("deceit-sculpture", "a wooden Deceit sculpture", ArtisanKind.Sculpture, "Deceit", (40579, 40442)),
            P("hythloth-sculpture", "a wooden crescent moon sculpture", ArtisanKind.Sculpture, "Hythloth", (41056, 41057)),
            P("honor-sculpture", "a wooden chalice sculpture", ArtisanKind.Sculpture, "Honor", (41061, 41062)),
            P("destard-sculpture", "a wooden skull sculpture", ArtisanKind.Sculpture, "Destard", (41066, 41067)),
            P("sacrifice-sculpture", "a wooden tear drop sculpture", ArtisanKind.Sculpture, "Sacrifice", (41071, 41072)),
            P("honesty-sculpture", "a wooden Honesty sculpture", ArtisanKind.Sculpture, "Honesty", (41554, 41555)),
            P("compassion-sculpture", "a wooden Compassion sculpture", ArtisanKind.Sculpture, "Compassion", (41559, 41560)),
            P("wrong-sculpture", "a wooden Wrong sculpture", ArtisanKind.Sculpture, "Wrong", (41564, 41565)),
            P("shame-sculpture", "a wooden Shame sculpture", ArtisanKind.Sculpture, "Shame", (41569, 41570)),
            P("justice-sculpture", "a wooden balanced scales sculpture", ArtisanKind.Sculpture, "Justice", (42137, 42138)),
            P("humility-sculpture", "a wooden crooks sculpture", ArtisanKind.Sculpture, "Humility", (42142, 42143)),
            P("covetous-sculpture", "a wooden coin sculpture", ArtisanKind.Sculpture, "Covetous", (42147, 42148)),
            P("pride-sculpture", "a wooden sceptre sculpture", ArtisanKind.Sculpture, "Pride", (42152, 42153)),
            P("compassion-pillow", "a Compassion throw pillow", ArtisanKind.Pillow, "Compassion", (40477, 0), (40478, 0)),
            P("honesty-pillow", "an Honesty throw pillow", ArtisanKind.Pillow, "Honesty", (40479, 0), (40480, 0)),
            P("shame-pillow", "a Shame throw pillow", ArtisanKind.Pillow, "Shame", (40481, 0), (40482, 0)),
            P("wrong-pillow", "a Wrong throw pillow", ArtisanKind.Pillow, "Wrong", (40483, 0), (40484, 0)),
            P("deceit-pillow", "a pair of masks throw pillow", ArtisanKind.Pillow, "Deceit", (41113, 0), (41114, 0)),
            P("despise-pillow", "an axe throw pillow", ArtisanKind.Pillow, "Despise", (41115, 0), (41116, 0)),
            P("valor-pillow", "a sword throw pillow", ArtisanKind.Pillow, "Valor", (41117, 0), (41118, 0)),
            P("spirituality-pillow", "an ankh throw pillow", ArtisanKind.Pillow, "Spirituality", (41119, 0), (41120, 0)),
            P("humility-pillow", "a pair of crooks throw pillow", ArtisanKind.Pillow, "Humility", (41546, 0), (41547, 0)),
            P("justice-pillow", "a balanced scales throw pillow", ArtisanKind.Pillow, "Justice", (41548, 0), (41549, 0)),
            P("pride-pillow", "a sceptre throw pillow", ArtisanKind.Pillow, "Pride", (41550, 0), (41551, 0)),
            P("covetous-pillow", "a coin throw pillow", ArtisanKind.Pillow, "Covetous", (41552, 0), (41553, 0)),
            P("honor-pillow", "an Honor throw pillow", ArtisanKind.Pillow, "Honor", (42129, 0), (42130, 0)),
            P("sacrifice-pillow", "a Sacrifice throw pillow", ArtisanKind.Pillow, "Sacrifice", (42131, 0), (42132, 0)),
            P("hythloth-pillow", "a Hythloth throw pillow", ArtisanKind.Pillow, "Hythloth", (42133, 0), (42134, 0)),
            P("destard-pillow", "a Destard throw pillow", ArtisanKind.Pillow, "Destard", (42135, 0), (42136, 0)),
            P("covetous-tapestry", "a Covetous tapestry", ArtisanKind.Tapestry, "Covetous", (40493, 0), (40494, 0)),
            P("pride-tapestry", "a Pride tapestry", ArtisanKind.Tapestry, "Pride", (40495, 0), (40496, 0)),
            P("humility-tapestry", "a Humility tapestry", ArtisanKind.Tapestry, "Humility", (40497, 0), (40498, 0)),
            P("justice-tapestry", "a Justice tapestry", ArtisanKind.Tapestry, "Justice", (40499, 0), (40500, 0)),
            P("compassion-tapestry", "a heart tapestry", ArtisanKind.Tapestry, "Compassion", (41124, 0), (41123, 0)),
            P("shame-tapestry", "a broken shield tapestry", ArtisanKind.Tapestry, "Shame", (41126, 0), (41125, 0)),
            P("honesty-tapestry", "a hand tapestry", ArtisanKind.Tapestry, "Honesty", (41128, 0), (41127, 0)),
            P("wrong-tapestry", "a broken scales tapestry", ArtisanKind.Tapestry, "Wrong", (41130, 0), (41129, 0)),
            P("hythloth-tapestry", "a Hythloth tapestry", ArtisanKind.Tapestry, "Hythloth", (41539, 0), (41538, 0)),
            P("sacrifice-tapestry", "a Sacrifice tapestry", ArtisanKind.Tapestry, "Sacrifice", (41542, 0), (41543, 0)),
            P("honor-tapestry", "an Honor drop tapestry", ArtisanKind.Tapestry, "Honor", (41544, 0), (41545, 0)),
            P("destard-tapestry", "a skull tapestry", ArtisanKind.Tapestry, "Destard", (41540, 0), (41541, 0)),
            P("despise-tapestry", "an axe tapestry", ArtisanKind.Tapestry, "Despise", (42121, 0), (42122, 0)),
            P("valor-tapestry", "a sword tapestry", ArtisanKind.Tapestry, "Valor", (42123, 0), (42124, 0)),
            P("spirituality-tapestry", "an ankh tapestry", ArtisanKind.Tapestry, "Spirituality", (42125, 0), (42126, 0)),
            P("deceit-tapestry", "a pair of masks tapestry", ArtisanKind.Tapestry, "Deceit", (42127, 0), (42128, 0)),
        ],
    };

    // -- The file -----------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(ArtisanFestivalTable table) => JsonSerializer.Serialize(table, Json);

    public static ArtisanFestivalTable Parse(string json, string source, List<string> errors)
    {
        ArtisanFestivalTable table;
        try
        {
            table = JsonSerializer.Deserialize<ArtisanFestivalTable>(json, Json);
        }
        catch (JsonException ex)
        {
            errors.Add($"{source}: not valid JSON: {ex.Message}");
            return null;
        }

        if (table == null)
        {
            errors.Add($"{source}: empty");
            return null;
        }

        table.Source = source;
        Validate(table, errors);
        return errors.Count == 0 ? table : null;
    }

    public static void Validate(ArtisanFestivalTable t, List<string> errors)
    {
        if (!Enum.TryParse<LightType>(t.Light ?? "", true, out _)) errors.Add($"light \"{t.Light}\" is not a light type");
        if (t.BoxWeight is < 0 or > 100) errors.Add($"boxWeight {t.BoxWeight} is not 0 to 100");

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new Dictionary<int, string>();
        foreach (var p in t.Pieces ?? [])
        {
            if (string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Name)) errors.Add("pieces: a key and a name are needed");
            else if (!keys.Add(p.Key)) errors.Add($"pieces: \"{p.Key}\" appears twice");

            var want = p.Kind == ArtisanKind.Sculpture ? 1 : 2;
            if (p.Facings == null || p.Facings.Count != want) errors.Add($"pieces: {p.Key} ({p.Kind}) needs {want} facing(s)");
            foreach (var f in p.Facings ?? [])
            {
                if (f.Id is < 1 or > 0xFFFF) errors.Add($"pieces: {p.Key}: item id {f.Id} is not 1 to 65535");
                if (p.IsLight && f.Lit is < 1 or > 0xFFFF) errors.Add($"pieces: {p.Key} is a light and needs a lit id");
                if (!p.IsLight && f.Lit != 0) errors.Add($"pieces: {p.Key} is not a light; lit must be 0");
                foreach (var id in new[] { f.Id, f.Lit }.Where(x => x != 0))
                    if (!ids.TryAdd(id, p.Key) && ids[id] != p.Key) errors.Add($"pieces: id {id} is used by {ids[id]} and {p.Key}");
            }
        }

        var stocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in t.Stock ?? [])
        {
            if (!keys.Contains(s.Key ?? "")) errors.Add($"stock: \"{s.Key}\" is not a piece");
            else if (!stocked.Add(s.Key)) errors.Add($"stock: \"{s.Key}\" appears twice");
            if (s.Price is < 1 or > 1000) errors.Add($"stock: {s.Key} price {s.Price} is not 1 to 1000 tokens");
        }
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        var lines = new List<string>();
        if (!File.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, ToJson(Defaults));
                lines.Add($"wrote the default table to {path}");
            }
            catch (Exception ex)
            {
                lines.Add($"could not write {path}: {ex.Message}; the built-in defaults are in force");
            }

            Current = Defaults;
            return lines;
        }

        lines.AddRange(Reload(path));
        return lines;
    }

    public static List<string> Reload(string path)
    {
        var lines = new List<string>();
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            lines.Add($"could not read {path}: {ex.Message}; the table in force is kept ({Current.Source})");
            return lines;
        }

        var errors = new List<string>();
        var table = Parse(json, path, errors);
        if (table == null)
        {
            lines.Add($"{path} refused; the table in force is kept ({Current.Source}):");
            lines.AddRange(errors.Select(e => "  " + e));
            return lines;
        }

        Current = table;
        lines.Add($"artisan festival table in force: {path} ({table.Pieces.Count} pieces, {table.Stock.Count} in the spring stock)");
        return lines;
    }
}
