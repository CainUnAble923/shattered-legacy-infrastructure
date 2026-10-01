// GuildDirectoryRowsVerification.cs
//
// cc-P23, D41: the Guild Directory listed the Artificers' Order twice. It drew one row per entry of
// GuildLocations.All (18 entries for 17 guilds: the Artificers stand in New Haven and in Ter Mur), so
// a guild with guildmasters in two towns was two rows. Rows are now keyed on the registered guilds,
// and "Show me the way" picks a guildmaster when it is pressed: the nearest on the player's own map,
// else one on another map, naming the town. Notes in shard-migration notes/cc-P23-live-defect-sweep.md.
//
// Facts (the brief's numbering). Each reads the gump as built and presses its own buttons:
//   1. A world with two guildmasters for one guild renders one row for it.
//   2. With seventeen guilds registered, the directory renders seventeen rows whatever the NPC count.
//   3. "Show me the way" from Trammel prefers a Trammel guildmaster when one exists, and names the town.
//   4. A guild whose guildmaster is deleted renders and reports the absence without an arrow.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildDirectoryRowsVerification
{
    private readonly ITestOutputHelper _out;

    public GuildDirectoryRowsVerification(ITestOutputHelper output)
    {
        _out = output;
        ClusterFGuildSystem.EnsureRegistered();
    }

    // ---------------------------------------------------------------- helpers

    // The directory's rows as built: each row's guild name (the label at x 20 that opens the row) and
    // its "Show me the way" reply button, in entry order across every page.
    private static List<(string Name, int Way)> Rows(GuildProgressGump gump)
    {
        var rows = new List<(string, int)>();
        string name = null;

        foreach (var entry in gump.Entries)
        {
            if (entry is GumpLabel { X: 20 } label && label.Y >= 92)
            {
                name = label.Text;
            }
            else if (entry is GumpButton { Type: GumpButtonType.Reply } button &&
                     button.ButtonID >= GuildProgressGump.BtnWayBase &&
                     button.ButtonID < GuildProgressGump.BtnServicesBase)
            {
                rows.Add((name, button.ButtonID));
            }
        }

        return rows;
    }

    private static PlayerMobile Online(Point3D where, Map map, out NetState ns)
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Server.Items.Backpack());
        pm.MoveToWorld(where, map);
        ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        return pm;
    }

    private static void Offline(PlayerMobile pm)
    {
        pm.QuestArrow = null;
        var ns = pm.NetState;
        pm.NetState = null;

        if (ns != null)
        {
            ns.Mobile = null;
            ns.Dispose();
        }

        pm.Delete();
    }

    // The status label (x 450) of the row whose name label reads `name`.
    private static string RowStatus(GuildProgressGump gump, string name)
    {
        var inRow = false;

        foreach (var entry in gump.Entries)
        {
            if (entry is not GumpLabel label)
            {
                continue;
            }

            if (label.X == 20 && label.Y >= 92)
            {
                inRow = label.Text == name;
            }
            else if (inRow && label.X == 450)
            {
                return label.Text;
            }
        }

        return null;
    }

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        return span.IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0 ||
               span.IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
    }

    private static void Press(GuildProgressGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    private static GuildLocation Location(string key, int mapIndex) =>
        GuildLocations.For(key).Single(l => l.MapIndex == mapIndex);

    private static T Place<T>(GuildLocation loc) where T : Mobile, new()
    {
        var npc = new T();
        npc.MoveToWorld(loc.Point, loc.Map);
        return npc;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AGuildWithTwoGuildmastersIsOneRow()
    {
        var trammel = Location("artificers", GuildLocation.TrammelIndex);
        var one = Place<ArtificersGuildmaster>(trammel);
        var two = new ArtificersGuildmaster();
        two.MoveToWorld(new Point3D(trammel.Point.X + 2, trammel.Point.Y, trammel.Point.Z), Map.Trammel);
        var pm = Online(new Point3D(trammel.Point.X - 4, trammel.Point.Y, trammel.Point.Z), Map.Trammel, out _);

        try
        {
            var rows = Rows(new GuildProgressGump(pm, null, null));
            var name = ClusterFGuildSystem.GetDef("artificers")!.Name;
            _out.WriteLine($"rows: {rows.Count}; {name}: {rows.Count(r => r.Name == name)}");
            Assert.Single(rows, r => r.Name == name);
        }
        finally
        {
            one.Delete();
            two.Delete();
            Offline(pm);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void SeventeenGuildsAreSeventeenRowsWhateverTheNpcCount()
    {
        Assert.Equal(17, ClusterFGuildSystem.AllGuilds.Count);

        var pm = Online(new Point3D(3500, 2570, 14), Map.Trammel, out _);
        var placed = new List<Mobile>();

        try
        {
            void Check(string when)
            {
                var rows = Rows(new GuildProgressGump(pm, null, null));
                _out.WriteLine($"{when}: {rows.Count} rows, {placed.Count} guildmasters placed by this fact");
                Assert.Equal(ClusterFGuildSystem.AllGuilds.Count, rows.Count);
                Assert.Equal(ClusterFGuildSystem.AllGuilds.Values.Select(d => d.Name).OrderBy(n => n),
                    rows.Select(r => r.Name).OrderBy(n => n));
                Assert.Equal(rows.Count, rows.Select(r => r.Way).Distinct().Count());
            }

            Check("no guildmasters placed");

            // Every location's guildmaster, twice over.
            foreach (var loc in GuildLocations.All)
            {
                if (loc.Map == null)
                {
                    continue;
                }

                for (var n = 0; n < 2; n++)
                {
                    var npc = (Mobile)Activator.CreateInstance(loc.NpcType)!;
                    npc.MoveToWorld(new Point3D(loc.Point.X + n, loc.Point.Y, loc.Point.Z), loc.Map);
                    placed.Add(npc);
                }
            }

            Check("two of every guildmaster");
        }
        finally
        {
            foreach (var npc in placed)
            {
                npc.Delete();
            }

            Offline(pm);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ShowMeTheWayFromTrammelPrefersATrammelGuildmasterAndNamesTheTown()
    {
        var trammel = Location("artificers", GuildLocation.TrammelIndex);
        var terMur = Location("artificers", GuildLocation.TerMurIndex);
        var name = ClusterFGuildSystem.GetDef("artificers")!.Name;

        var home = Place<ArtificersGuildmaster>(trammel);
        var away = terMur.Map != null ? Place<ArtificersGuildmaster>(terMur) : null;
        _out.WriteLine(away != null ? "Ter Mur is loaded in this host." : "Ter Mur is not loaded in this host.");

        var pm = Online(new Point3D(trammel.Point.X - 6, trammel.Point.Y, trammel.Point.Z), Map.Trammel, out var ns);

        try
        {
            var gump = new GuildProgressGump(pm, null, null);
            var way = Assert.Single(Rows(gump), r => r.Name == name).Way;

            var mark = Mark(ns);
            Press(gump, ns, way);
            var arrow = Assert.IsType<GuildDirectionArrow>(pm.QuestArrow);
            Assert.Same(home, arrow.Target);
            Assert.True(SentText(ns, mark, "in New Haven"), "the message does not name the town");

            // The Trammel guildmaster gone: the one in Ter Mur is named, and no arrow crosses facets.
            pm.QuestArrow = null;
            home.Delete();

            if (away != null)
            {
                mark = Mark(ns);
                Press(new GuildProgressGump(pm, null, null), ns, way);
                Assert.Null(pm.QuestArrow);
                Assert.True(SentText(ns, mark, "in Ter Mur"), "the fallback does not name Ter Mur");
                Assert.True(SentText(ns, mark, "cannot cross facets"));
            }
        }
        finally
        {
            home.Delete();
            away?.Delete();
            Offline(pm);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AGuildWithNoGuildmasterRendersAndSaysSoWithoutAnArrow()
    {
        var warriors = Location("warriors", GuildLocation.TrammelIndex);
        var name = ClusterFGuildSystem.GetDef("warriors")!.Name;

        var npc = Place<WarriorGuildmaster>(warriors);
        npc.Delete();

        var pm = Online(new Point3D(warriors.Point.X - 5, warriors.Point.Y, warriors.Point.Z), Map.Trammel, out var ns);

        try
        {
            var gump = new GuildProgressGump(pm, null, null);
            var row = Assert.Single(Rows(gump), r => r.Name == name);
            Assert.Equal("Guildmaster missing", RowStatus(gump, name));

            var mark = Mark(ns);
            Press(gump, ns, row.Way);
            Assert.Null(pm.QuestArrow);
            Assert.True(SentText(ns, mark, "guildmaster is not at"), "the absence is not reported in P15's words");
            Assert.True(SentText(ns, mark, "Please tell a Game Master"));
        }
        finally
        {
            Offline(pm);
        }
    }
}
