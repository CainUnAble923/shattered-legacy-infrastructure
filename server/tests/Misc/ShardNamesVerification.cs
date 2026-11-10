// ShardNamesVerification.cs
//
// cc-P861: our extra NPC name lists and the town rule (server/customizations/Names/ShardNames.cs,
// server/data/names-shard.json). Notes in shard-migration notes/cc-P861-names-and-statify.md.
//
// Facts:
//   1. The shipped file: fifteen rows credited to Sirenix; every "new" row is a list pinned's names.json lacks and every
//      "extend" row one it has; names non-empty, unique in their list, plain ASCII letters, a capital first (titles
//      start "the").
//   2. Apply adds the new lists to pinned's table (RandomName draws from them), extends a list only by the names it
//      lacks, refuses to extend a list pinned lacks, and a second apply changes nothing.
//   3. In one town, people who share a name drawn from a list are renamed but the oldest, each to a name from the same
//      list nobody in that town has; the same name in another town, a fixed name (not in a list), a pet and a player
//      are left alone; a dry run renames nothing; a second sweep renames nothing.
//   4. The gargish outcast takes its name from the gargoyle male or female list, by sex, as in ServUO.
//
// The test host loads neither pinned's names.json nor ours, so the facts apply them, and put the table back after.
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Misc;
using Server.Mobiles;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShardNamesVerification : IDisposable
{
    // An empty corner of Trammel for two towns of the facts' own.
    private static readonly Rectangle3D TownA = new(new Point3D(5160, 1160, -128), new Point3D(5170, 1170, 127));
    private static readonly Rectangle3D TownB = new(new Point3D(5180, 1160, -128), new Point3D(5190, 1170, 127));

    private readonly ITestOutputHelper _out;
    private readonly Dictionary<string, NameList> _table;
    private readonly Dictionary<string, string[]> _before;
    private readonly List<Region> _regions = [];
    private readonly List<Mobile> _made = [];

    public ShardNamesVerification(ITestOutputHelper output)
    {
        _out = output;
        _table = (Dictionary<string, NameList>)typeof(NameList)
            .GetField("m_Table", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        _before = _table.ToDictionary(kv => kv.Key, kv => kv.Value.List, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        foreach (var m in _made)
        {
            m.Delete();
        }

        foreach (var r in _regions)
        {
            r.Unregister();
        }

        // The table as it was: lists added here go, lists changed here get their names back.
        foreach (var key in _table.Keys.ToList())
        {
            if (!_before.TryGetValue(key, out var names))
            {
                _table.Remove(key);
            }
            else
            {
                _table[key].List = names;
            }
        }
    }

    private static List<(string Type, string[] Names)> PinnedRows()
    {
        var path = ShardNames.FindDataFile("names.json", ours: false);
        Assert.True(path != null, "pinned's Data/names.json not found above the test host");
        using var doc = JsonDocument.Parse(File.ReadAllText(path!));
        return doc.RootElement.EnumerateArray()
            .Select(e => (e.GetProperty("type").GetString()!, e.GetProperty("names").EnumerateArray().Select(n => n.GetString()!).ToArray()))
            .ToList();
    }

    // Pinned's lists in the table, as the live server has them after NameList.Configure.
    private void LoadPinned()
    {
        foreach (var (type, names) in PinnedRows())
        {
            if (!_table.ContainsKey(type))
            {
                _table[type] = new NameList { Type = type, List = names.Select(n => n.Trim()).ToArray() };
            }
        }
    }

    private static ShardNameFile Shipped()
    {
        var path = ShardNames.FindDataFile(ShardNames.FileName);
        Assert.True(path != null, "names-shard.json not shipped to Distribution/Data/ShatteredLegacy");
        return ShardNames.Read(path!);
    }

    private BaseCreature Person(string name, Point3D at, bool female = false, Action<BaseCreature>? set = null)
    {
        var m = new BaseVendorStandIn { Name = name, Female = female, Body = female ? 0x191 : 0x190 };
        set?.Invoke(m);
        m.MoveToWorld(at, Map.Trammel);
        _made.Add(m);
        return m;
    }

    private void Towns()
    {
        foreach (var (name, area) in new[] { ("p861 town A", TownA), ("p861 town B", TownB) })
        {
            var r = new TownRegion(name, Map.Trammel, Region.DefaultPriority + 20, area);
            r.Register();
            _regions.Add(r);
        }
    }

    // ---- 1 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheShippedFileIsSirenixsListsCleanAndBesidePinneds()
    {
        var file = Shipped();
        var pinned = PinnedRows().Select(r => r.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Sirenix", file.Credit);
        Assert.Equal(15, file.Lists.Count);
        Assert.Equal(12, file.Lists.Count(r => r.Mode == "new"));
        Assert.Equal(3, file.Lists.Count(r => r.Mode == "extend"));

        foreach (var row in file.Lists)
        {
            _out.WriteLine($"{row.Type} ({row.Mode}): {row.Names.Length}");
            Assert.True(row.Mode == "new" ? !pinned.Contains(row.Type) : pinned.Contains(row.Type), row.Type);
            Assert.NotEmpty(row.Names);
            Assert.Equal(row.Names.Length, row.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var n in row.Names)
            {
                Assert.Matches("^[A-Za-z][A-Za-z' -]*$", n);
                Assert.True(row.Type.EndsWith("title") ? n.StartsWith("the ") : char.IsUpper(n[0]), $"{row.Type}: '{n}'");
            }
        }

        foreach (var t in new[] { "elf male", "elf female", "gargoyle male", "gargoyle female", "druid", "paladin", "juka", "meer", "gladiator male" })
        {
            Assert.Contains(file.Lists, r => r.Type == t && r.Mode == "new");
        }
    }

    // ---- 2 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ApplyAddsAndExtendsOnceAndRefusesWhatPinnedLacks()
    {
        LoadPinned();
        var file = Shipped();
        var pinnedVendors = _table["gargoyle vendor"].List.ToArray();

        var first = ShardNames.Apply(file);
        first.ForEach(_out.WriteLine);

        var juka = NameList.GetNameList("juka");
        Assert.NotNull(juka);
        Assert.Equal(file.Lists.Single(r => r.Type == "juka").Names, juka!.List);
        Assert.True(juka.ContainsName(NameList.RandomName("juka")));

        var vendors = _table["gargoyle vendor"].List;
        var extra = file.Lists.Single(r => r.Type == "gargoyle vendor").Names;
        Assert.Equal(pinnedVendors.Length + extra.Length, vendors.Length);
        Assert.Equal(pinnedVendors, vendors.Take(pinnedVendors.Length));          // pinned's names kept, first
        Assert.Equal(vendors.Length, vendors.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var counts = _table.ToDictionary(kv => kv.Key, kv => kv.Value.List.Length);
        var second = ShardNames.Apply(file);
        Assert.Contains(second, l => l.StartsWith("0 list(s) added, 0 extended, 0 name(s) new"));
        Assert.Equal(counts, _table.ToDictionary(kv => kv.Key, kv => kv.Value.List.Length));

        var bad = new ShardNameFile { Credit = "test", Lists = [new ShardNameRow { Type = "no such list", Mode = "extend", Names = ["Abe"] }] };
        Assert.Contains(ShardNames.Apply(bad), l => l.StartsWith("refused"));
        Assert.Null(NameList.GetNameList("no such list"));
    }

    // ---- 3 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void InOneTownTheRepeatsAreRenamedButTheOldestAndNothingElse()
    {
        ShardNames.Apply(Shipped());
        Towns();
        var gladiators = NameList.GetNameList("gladiator male")!;
        var name = gladiators.List[0];

        var oldest = Person(name, new Point3D(5162, 1162, 0));
        var second = Person(name, new Point3D(5163, 1162, 0));
        var third = Person(name, new Point3D(5164, 1162, 0));
        var elsewhere = Person(name, new Point3D(5182, 1162, 0));               // town B
        var fixedA = Person("Elara Voss", new Point3D(5165, 1163, 0));
        var fixedB = Person("Elara Voss", new Point3D(5166, 1163, 0));
        var pet = Person(name, new Point3D(5167, 1163, 0), set: m => m.Controlled = true);
        var all = new List<BaseCreature> { oldest, second, third, elsewhere, fixedA, fixedB, pet };

        Assert.Equal("gladiator male", ShardNames.ListOf(oldest));
        Assert.Null(ShardNames.ListOf(fixedA));

        var dry = ShardNames.DedupeAmong(all, true, out var toRename);
        dry.ForEach(_out.WriteLine);
        Assert.Equal(2, toRename);
        Assert.Equal(name, second.Name);                                        // a dry run renames nothing

        ShardNames.DedupeAmong(all, false, out var renamed);
        Assert.Equal(2, renamed);
        Assert.Equal(name, oldest.Name);
        Assert.NotEqual(name, second.Name);
        Assert.NotEqual(name, third.Name);
        Assert.NotEqual(second.Name, third.Name);
        Assert.True(gladiators.ContainsName(second.Name) && gladiators.ContainsName(third.Name));
        Assert.Equal(name, elsewhere.Name);                                     // another town may share it
        Assert.Equal("Elara Voss", fixedA.Name);                                // fixed identities stay
        Assert.Equal("Elara Voss", fixedB.Name);
        Assert.Equal(name, pet.Name);                                           // a pet is not a townsperson

        ShardNames.DedupeAmong(all, false, out var again);
        Assert.Equal(0, again);
    }

    // ---- 4 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheGargishOutcastIsNamedFromTheGargoyleListsBySex()
    {
        ShardNames.Apply(Shipped());
        var male = NameList.GetNameList("gargoyle male")!;
        var female = NameList.GetNameList("gargoyle female")!;

        for (var i = 0; i < 12; i++)
        {
            var g = new GargishOutcast();
            _made.Add(g);
            Assert.True((g.Female ? female : male).ContainsName(g.Name), $"{(g.Female ? "female" : "male")} outcast named '{g.Name}'");
        }
    }

    // A plain townsperson: a creature with no AI of its own to wander off or name itself.
    private sealed class BaseVendorStandIn : BaseCreature
    {
        public BaseVendorStandIn() : base(AIType.AI_Animal, FightMode.None)
        {
            CantWalk = true;
            Blessed = true;
        }

        public BaseVendorStandIn(Serial serial) : base(serial)
        {
        }
    }
}
