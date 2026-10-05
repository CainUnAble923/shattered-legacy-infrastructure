// cc-P53 Parts D and E, the craft gump.
//
// Part E (D63): pinned's category list has no paging (CraftGump.CreateGroupList) and its panel ends at y 287, above
// NOTICES; blacksmithy's twelfth group drew over the NOTICES label. server/patches/CraftGump-category-pages.patch pages
// the list past ten groups. Facts: for every craft system, on every category page and with any group selected, no
// element of the category list reaches below the panel, every group can be clicked on some page, and the paging
// buttons move between pages.
//
// Part D (D62): server/customizations/Engines/Craft/CraftMaterialNames.cs replaces each stock cliloc material name
// ("IRON (~1_AMT~)") with its Title Case string. Facts: every system's sub-resource and scale names are Title Case
// strings with their types and required skills unchanged; the gump draws no material cliloc and shows "Dull Copper"
// on the selected-material line; and picking a material still selects that type.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh. Def*.Initialize() and ServerStarted do not run in the host, so Systems() runs them in
// the server's order, as CleanUpBritanniaPointsVerification does.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Server;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CraftCategoryPagingVerification
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
    private const int PanelBottom = 287; // CraftGump: the categories tile is y 37 + 250; NOTICES starts at 292

    private readonly ITestOutputHelper _out;

    public CraftCategoryPagingVerification(ITestOutputHelper output) => _out = output;

    private static readonly MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(NetState), typeof(BaseGump)], null);

    private static List<(string Name, CraftSystem System)> Systems()
    {
        if (DefAlchemy.CraftSystem == null) DefAlchemy.Initialize();
        if (DefBlacksmithy.CraftSystem == null) DefBlacksmithy.Initialize();
        if (DefBowFletching.CraftSystem == null) DefBowFletching.Initialize();
        if (DefCarpentry.CraftSystem == null) DefCarpentry.Initialize();
        if (DefCartography.CraftSystem == null) DefCartography.Initialize();
        if (DefCooking.CraftSystem == null) DefCooking.Initialize();
        if (DefGlassblowing.CraftSystem == null) DefGlassblowing.Initialize();
        if (DefInscription.CraftSystem == null) DefInscription.Initialize();
        if (DefMasonry.CraftSystem == null) DefMasonry.Initialize();
        if (DefTailoring.CraftSystem == null) DefTailoring.Initialize();
        if (DefTinkering.CraftSystem == null) DefTinkering.Initialize();

        BlacksmithyCraftRegistrations.Register();
        TailoringCraftRegistrations.Register();
        TinkeringCraftRegistrations.Register();
        CarpentryCraftRegistrations.Register();

        if (DefBlacksmithy.CraftSystem.CraftSubRes.SearchFor(typeof(PlatinumIngot)) == null)
        {
            typeof(ClusterFMiningExtension).GetMethod("OnServerStarted", Private)!.Invoke(null, null);
        }

        if (DefCarpentry.CraftSystem.CraftSubRes.SearchFor(typeof(IronwoodBoard)) == null)
        {
            typeof(ClusterFLumberjackingExtension).GetMethod("OnServerStarted", Private)!.Invoke(null, null);
        }

        CraftMaterialNames.Apply();
        CraftMaterialNames.Apply(); // idempotent

        return new List<(string, CraftSystem)>
        {
            ("Alchemy", DefAlchemy.CraftSystem),
            ("Blacksmithy", DefBlacksmithy.CraftSystem),
            ("Bowcraft/Fletching", DefBowFletching.CraftSystem),
            ("Carpentry", DefCarpentry.CraftSystem),
            ("Cartography", DefCartography.CraftSystem),
            ("Cooking", DefCooking.CraftSystem),
            ("Glassblowing", DefGlassblowing.CraftSystem),
            ("Inscription", DefInscription.CraftSystem),
            ("Masonry", DefMasonry.CraftSystem),
            ("Tailoring", DefTailoring.CraftSystem),
            ("Tinkering", DefTinkering.CraftSystem)
        };
    }

    private sealed class Crafter : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Crafter()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(1240, 1240, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
        }
    }

    // The 0xDD packet for this gump: its layout, and its text lines.
    private static (string Layout, List<string> Lines) Read(NetState ns, int from, BaseGump gump)
    {
        var wire = ns.SendBuffer.GetReadSpan()[from..];
        var start = -1;
        for (var i = 0; i + 7 <= wire.Length; i++)
        {
            if (wire[i] == 0xDD && BinaryPrimitives.ReadUInt32BigEndian(wire[(i + 3)..]) == (uint)gump.Serial)
            {
                start = i;
            }
        }

        Assert.True(start >= 0, "no gump on the wire");
        wire = wire[start..];
        var pos = 19;
        var layout = Encoding.ASCII.GetString(Inflate(wire, ref pos));
        var lines = new List<string>();
        var count = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]);
        pos += 4;
        if (count > 0)
        {
            var text = Inflate(wire, ref pos);
            var p = 0;
            for (var i = 0; i < count; i++)
            {
                var len = BinaryPrimitives.ReadUInt16BigEndian(text.AsSpan(p));
                p += 2;
                lines.Add(Encoding.BigEndianUnicode.GetString(text, p, len * 2));
                p += len * 2;
            }
        }

        return (layout, lines);
    }

    private static byte[] Inflate(ReadOnlySpan<byte> wire, ref int pos)
    {
        var packed = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
        var length = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
        using var input = new MemoryStream(wire.Slice(pos + 8, packed).ToArray());
        pos += 8 + packed;
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        Assert.Equal(length, (int)output.Length);
        return output.ToArray();
    }

    private static (string Layout, List<string> Lines) Send(Crafter c, CraftGump gump)
    {
        var from = c.Ns.SendBuffer.GetReadSpan().Length;
        c.Pm.SendGump(gump);
        return Read(c.Ns, from, gump);
    }

    private record Element(string Kind, int X, int Y, int H, int Id);

    // The category column: everything left of the selections panel, below the CATEGORIES header, except the NOTICES
    // label (10, 302) and the bottom controls (y 342 and down).
    private static List<Element> CategoryColumn(string layout)
    {
        var list = new List<Element>();
        foreach (Match m in Regex.Matches(layout, @"\{ (\w+) ([^}]*)\}"))
        {
            var kind = m.Groups[1].Value;
            var a = m.Groups[2].Value.Trim().Split(' ');
            if (a.Length < 2 || !int.TryParse(a[0], out var x) || !int.TryParse(a[1], out var y))
            {
                continue;
            }

            if (x >= 210 || y < 60 || y >= 342 || x == 10 && y == 302)
            {
                continue;
            }

            // What a player reads or clicks; the panel tiles themselves are pinned's and unchanged.
            if (kind != "button" && kind != "text" && !kind.StartsWith("xmfhtml"))
            {
                continue;
            }

            var h = kind switch
            {
                "button" => 22,
                "text" => 18,
                _ => int.Parse(a[3])
            };
            var id = kind == "button" ? int.Parse(a[6]) : -1;
            list.Add(new Element(kind, x, y, h, id));
        }

        return list;
    }

    private static int GroupOf(int buttonId) => (buttonId - 1) % 7 == 0 ? (buttonId - 1) / 7 : -1;

    // ---------------------------------------------------------------- E1

    [Fact]
    public void NoCategoryRowReachesTheNoticesAreaAndEveryGroupIsClickable()
    {
        using var c = new Crafter();
        var tool = new SmithHammer();
        c.Pm.Backpack.DropItem(tool);

        foreach (var (name, system) in Systems())
        {
            var groups = system.CraftGroups.Count;
            var pages = CraftGump.GroupPageCount(groups);
            _out.WriteLine($"{name}: {groups} categories, {pages} page(s)");

            var reached = new HashSet<int>();
            var context = system.GetContext(c.Pm);

            // Every page as a paging button asks for it, and every selection as the derived page shows it.
            var views = new List<(int Selected, int Page)>();
            for (var p = 0; p < pages; p++)
            {
                views.Add((-1, p));
            }

            for (var g = 0; g < groups; g++)
            {
                views.Add((g, -1));
            }

            views.Add((501, -1)); // LAST TEN selected

            foreach (var (selected, page) in views)
            {
                context.LastGroupIndex = selected;
                var (layout, _) = Send(c, new CraftGump(c.Pm, system, tool, null, CraftGump.CraftPage.None, page));
                var shown = new HashSet<int>();

                foreach (var e in CategoryColumn(layout))
                {
                    Assert.True(e.Y + e.H <= PanelBottom,
                        $"{name} (selected {selected}, page {page}): {e.Kind} at y {e.Y} reaches {e.Y + e.H}, past {PanelBottom}");

                    if (e.Kind == "button" && GroupOf(e.Id) >= 0)
                    {
                        reached.Add(GroupOf(e.Id));
                        shown.Add(GroupOf(e.Id));
                    }
                }

                if (selected >= 0 && selected < groups)
                {
                    Assert.Contains(selected, shown); // the selected group's own page is the one shown
                }
            }

            Assert.Equal(Enumerable.Range(0, groups), reached.OrderBy(i => i));
        }

        tool.Delete();
    }

    // ---------------------------------------------------------------- E2

    [Fact]
    public void ThePagingButtonsMoveBetweenPages()
    {
        using var c = new Crafter();
        var tool = new SmithHammer();
        c.Pm.Backpack.DropItem(tool);
        var smith = Systems().Single(s => s.Name == "Blacksmithy").System;
        Assert.True(CraftGump.GroupPageCount(smith.CraftGroups.Count) > 1, "blacksmithy needs more than one page");

        smith.GetContext(c.Pm).LastGroupIndex = 0;
        var gump = new CraftGump(c.Pm, smith, tool, null);
        var (first, _) = Send(c, gump);
        var next = CraftGump.GetButtonID(6, 10);
        Assert.Contains($"4005 4007 1 0 {next} }}", first);
        Assert.DoesNotContain($" {CraftGump.GetButtonID(6, 9)} }}", first);

        GumpRemove.Invoke(null, [c.Ns, gump]);
        var from = c.Ns.SendBuffer.GetReadSpan().Length;
        gump.OnResponse(c.Ns, new RelayInfo(next, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

        var second = c.Pm.GetGumps().Find<CraftGump>();
        var (layout, lines) = Read(c.Ns, from, second);
        Assert.Contains($" {CraftGump.GetButtonID(0, CraftGump.GroupsPerPage)} }}", layout); // first group of page 2
        Assert.Contains($" {CraftGump.GetButtonID(6, 9)} }}", layout); // and a way back
        Assert.Contains(lines, l => l.StartsWith("Page 2 of "));
        Assert.Equal(0, smith.GetContext(c.Pm).LastGroupIndex); // paging selects nothing

        tool.Delete();
    }

    // ---------------------------------------------------------------- D1

    private record Sub(Type Type, string Name, double Req);

    private static Sub[] Ingots(string granite = null) =>
    [
        new(granite == null ? typeof(IronIngot) : typeof(Granite), granite ?? "Iron", 0.0),
        new(granite == null ? typeof(DullCopperIngot) : typeof(DullCopperGranite), "Dull Copper", 65.0),
        new(granite == null ? typeof(ShadowIronIngot) : typeof(ShadowIronGranite), "Shadow Iron", 70.0),
        new(granite == null ? typeof(CopperIngot) : typeof(CopperGranite), "Copper", 75.0),
        new(granite == null ? typeof(BronzeIngot) : typeof(BronzeGranite), "Bronze", 80.0),
        new(granite == null ? typeof(GoldIngot) : typeof(GoldGranite), "Gold", 85.0),
        new(granite == null ? typeof(AgapiteIngot) : typeof(AgapiteGranite), "Agapite", 90.0),
        new(granite == null ? typeof(VeriteIngot) : typeof(VeriteGranite), "Verite", 95.0),
        new(granite == null ? typeof(ValoriteIngot) : typeof(ValoriteGranite), "Valorite", 99.0)
    ];

    private static readonly Sub[] Woods =
    [
        new(typeof(Log), "Wood", 0.0), new(typeof(OakLog), "Oak", 65.0), new(typeof(AshLog), "Ash", 80.0),
        new(typeof(YewLog), "Yew", 95.0), new(typeof(HeartwoodLog), "Heartwood", 100.0),
        new(typeof(BloodwoodLog), "Bloodwood", 100.0), new(typeof(FrostwoodLog), "Frostwood", 100.0)
    ];

    private void AssertStock(string system, CraftSubResCol col, string colName, Sub[] stock)
    {
        _out.WriteLine($"{system}: {col.Name.String} | {string.Join(", ", col.Select(r => $"{r.Name.String} {r.RequiredSkill}"))}");
        Assert.Equal(0, col.Name.Number);
        Assert.Equal(colName, col.Name.String);

        for (var i = 0; i < stock.Length; i++)
        {
            Assert.Equal(stock[i].Type, col[i].ItemType);
            Assert.Equal(stock[i].Name, col[i].Name.String);
            Assert.Equal(stock[i].Req, col[i].RequiredSkill);
        }

        foreach (var r in col)
        {
            Assert.Equal(0, r.Name.Number);
            Assert.Matches(@"^[A-Z][a-z]+([ /][A-Z][a-z]+)*$", r.Name.String);
        }
    }

    [Fact]
    public void EverySystemsMaterialAndScaleNamesAreTitleCaseStrings()
    {
        Systems();

        AssertStock("Blacksmithy", DefBlacksmithy.CraftSystem.CraftSubRes, "Iron", Ingots());
        AssertStock("Blacksmithy scales", DefBlacksmithy.CraftSystem.CraftSubRes2, "Red Scales",
        [
            new(typeof(RedScales), "Red Scales", 0.0), new(typeof(YellowScales), "Yellow Scales", 0.0),
            new(typeof(BlackScales), "Black Scales", 0.0), new(typeof(GreenScales), "Green Scales", 0.0),
            new(typeof(WhiteScales), "White Scales", 0.0), new(typeof(BlueScales), "Blue Scales", 0.0)
        ]);
        AssertStock("Tinkering", DefTinkering.CraftSystem.CraftSubRes, "Iron", Ingots());
        AssertStock("Carpentry", DefCarpentry.CraftSystem.CraftSubRes, "Wood", Woods);
        AssertStock("Bowcraft/Fletching", DefBowFletching.CraftSystem.CraftSubRes, "Wood", Woods);
        AssertStock("Tailoring", DefTailoring.CraftSystem.CraftSubRes, "Leather/Hides",
        [
            new(typeof(Leather), "Leather/Hides", 0.0), new(typeof(SpinedLeather), "Spined Hides", 65.0),
            new(typeof(HornedLeather), "Horned Hides", 80.0), new(typeof(BarbedLeather), "Barbed Hides", 99.0)
        ]);
        AssertStock("Masonry", DefMasonry.CraftSystem.CraftSubRes, "Normal", Ingots("Normal"));

        // Ours are untouched and still last.
        Assert.Equal("Celestial", DefBlacksmithy.CraftSystem.CraftSubRes[^1].Name.String);
        Assert.Equal("Starwood", DefCarpentry.CraftSystem.CraftSubRes[^1].Name.String);

        // No system that has no materials gained any.
        foreach (var (name, system) in Systems())
        {
            foreach (var col in new[] { system.CraftSubRes, system.CraftSubRes2 })
            {
                Assert.All(col, r => Assert.Equal(0, r.Name.Number));
            }
        }
    }

    // ---------------------------------------------------------------- D2

    [Fact]
    public void TheGumpDrawsNoMaterialClilocAndPickingOneSelectsItsType()
    {
        using var c = new Crafter();
        var tool = new SmithHammer();
        c.Pm.Backpack.DropItem(tool);
        c.Pm.Backpack.DropItem(new DullCopperIngot(50));
        c.Pm.Skills.Blacksmith.Base = 100.0;
        var smith = Systems().Single(s => s.Name == "Blacksmithy").System;
        var context = smith.GetContext(c.Pm);
        context.LastResourceIndex = -1;
        context.LastResourceIndex2 = -1;

        var picker = new CraftGump(c.Pm, smith, tool, null, CraftGump.CraftPage.PickResource);
        var (pickLayout, pickLines) = Send(c, picker);
        foreach (var number in CraftMaterialNames.Names.Keys)
        {
            Assert.DoesNotContain($" {number} ", pickLayout);
        }

        Assert.Contains("Dull Copper (50)", pickLines);
        Assert.Contains("Iron (0)", pickLines);

        var index = smith.CraftSubRes.FindIndex(r => r.ItemType == typeof(DullCopperIngot));
        GumpRemove.Invoke(null, [c.Ns, picker]);
        var from = c.Ns.SendBuffer.GetReadSpan().Length;
        picker.OnResponse(c.Ns, new RelayInfo(CraftGump.GetButtonID(5, index), ReadOnlySpan<int>.Empty,
            ReadOnlySpan<ushort>.Empty, ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

        Assert.Equal(index, context.LastResourceIndex);
        Assert.Equal(typeof(DullCopperIngot), smith.CraftSubRes.GetAt(context.LastResourceIndex).ItemType);

        var main = c.Pm.GetGumps().Find<CraftGump>();
        var (layout, lines) = Read(c.Ns, from, main);
        _out.WriteLine(string.Join(" | ", lines));
        Assert.Contains("Dull Copper (50 Available)", lines); // the selected-material line (CraftGump.cs:155)
        Assert.Contains("Red Scales (0 Available)", lines);
        foreach (var number in CraftMaterialNames.Names.Keys)
        {
            Assert.DoesNotContain($" {number} ", layout);
        }

        // And the craft uses it: pinned's resource check with the selected type.
        var dagger = smith.CraftItems.SearchFor(typeof(Dagger));
        var hue = 0;
        var max = 0;
        TextDefinition message = null;
        Assert.True(dagger.ConsumeRes(c.Pm, smith.CraftSubRes.GetAt(index).ItemType, smith, ref hue, ref max,
            ConsumeType.All, ref message));
        Assert.True(c.Pm.Backpack.GetAmount(typeof(DullCopperIngot)) < 50);

        context.LastResourceIndex = -1;
        tool.Delete();
    }
}
