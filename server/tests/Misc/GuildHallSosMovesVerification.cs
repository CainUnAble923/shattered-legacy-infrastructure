// GuildHallSosMovesVerification.cs
//
// cc-P708: six headquarters move onto the Siege of Sosaria land, Chase's picks of 2026-10-27 from P704 (the places are
// Zigholtul's). Notes in shard-migration notes/cc-P708-guild-hqs-on-sos.md. The map side (each moved hall's
// guildmaster, sign and travel stop on dry, reachable ground on the SoS file) is shard-migration
// tools/tests/test_guild_hqs_sos.py: the test host loads no client files.
//
// What the facts pin:
//   1. Each moved hall stands in the town Chase picked: the League and the Merchants in Valunstead, in separate
//      buildings; the Healers at Fort Whitestone; the Rangers at Fort Viridian; the Custodians and the Tinkers in
//      Hammerhill, in separate buildings. Every other hall stays where it was. ClusterFGuildHallMoves names exactly the
//      six, each with its old post.
//   2. The League's headquarters has its own steward, not a second Elara: Elara (LeagueRegistrar) stays at the New
//      Haven field office, and the steward sends anyone asking to join to her there.
//   3. place takes a moved hall's old site away: its old sign (showing again the stock sign it hid), an old guild house
//      and its doors, and the guildmaster at the old post; then places the hall at its new site, once. clear does the
//      same for an old site left behind. A same-type guildmaster elsewhere is left alone.
//   4. Joining needs no headquarters: each moved guild keeps its New Haven chapter, and a new character can join it
//      there with no hall placed anywhere.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Items;
using Server.Mobiles;
using Xunit;
using static Server.ClusterFGuildHallPlacementData;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildHallSosMovesVerification
{
    private static readonly Dictionary<string, string> Picks = new()
    {
        ["league"] = "Valunstead",
        ["merchants"] = "Valunstead",
        ["healers"] = "Fort Whitestone",
        ["rangers"] = "Fort Viridian",
        ["custodians"] = "Hammerhill",
        ["tinkers"] = "Hammerhill"
    };

    // what stood before P708, for the halls that stay
    private static readonly Dictionary<string, string> Stays = new()
    {
        ["warriors"] = "Jhelom", ["arcane"] = "Moonglow", ["mining"] = "Minoc", ["smithing"] = "Serpent's Hold",
        ["thieves"] = "Buccaneer's Den", ["dojo"] = "Zento", ["keepers"] = "Umbra", ["foresters"] = "Yew",
        ["tailoring"] = "Nujel'm", ["bards"] = "Britain", ["artificers"] = "Royal City", ["alembic"] = "Papua"
    };

    // New Haven in the author's Haven (cc-P678's town box)
    private static bool InNewHaven(Point3D p) => p.X is >= 3568 and <= 3761 && p.Y is >= 2460 and <= 2712;

    private static Hall H(string key) => ClusterFGuildHallPlacement.Find(key);

    private static void Clean()
    {
        ClusterFGuildHallPlacement.Clear(null, false, true);
        foreach (var i in World.Items.Values.Where(i => i is IGuildHallPart or GuildHallDecor).ToList())
        {
            i.Delete();
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EachMovedHallStandsInTheTownChasePicked()
    {
        foreach (var (key, town) in Picks)
        {
            Assert.True(H(key).Town == town, $"{key}: in {H(key).Town}, not {town}");
            Assert.Equal(1, H(key).MapIndex);
            Assert.Equal(Kind.Reuse, H(key).Kind);
            Assert.Contains("Zigholtul", H(key).Building);
        }

        foreach (var (key, town) in Stays)
        {
            Assert.True(H(key).Town == town, $"{key}: moved to {H(key).Town}");
        }

        Assert.Equal(Halls.Length, Picks.Count + Stays.Count);

        // separate buildings in the same town: no shared tile, and well apart
        static int Apart(Hall a, Hall b) => Math.Max(Math.Abs(a.Npc.X - b.Npc.X), Math.Abs(a.Npc.Y - b.Npc.Y));
        Assert.True(Apart(H("league"), H("merchants")) >= 20, "the League and the Merchants share a building");
        Assert.True(Apart(H("custodians"), H("tinkers")) >= 20, "the Custodians and the Tinkers share a building");

        Assert.Equal(Picks.Keys.OrderBy(k => k), ClusterFGuildHallMoves.All.Select(m => m.Key).OrderBy(k => k));
        foreach (var m in ClusterFGuildHallMoves.All)
        {
            Assert.NotEqual(m.Npc, H(m.Key).Npc);
            Assert.False(m.FromTown.Contains(H(m.Key).Town), m.Key);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheLeagueHeadquartersHasItsOwnStewardAndElaraStaysInNewHaven()
    {
        Assert.Equal(typeof(LeagueSteward), H("league").NpcType);
        Assert.NotEqual(typeof(LeagueRegistrar), H("league").NpcType);
        Assert.True(InNewHaven(ClusterFNewHavenSeeder.RegistrarAnchor), $"Elara at {ClusterFNewHavenSeeder.RegistrarAnchor}");

        var steward = new LeagueSteward();
        try
        {
            Assert.True(steward.IsInvulnerable);
            Assert.DoesNotContain("New Haven", steward.Name);
            Assert.Contains("Elara Voss", LeagueSteward.JoinLine);
            Assert.Contains("New Haven", LeagueSteward.JoinLine);
            Assert.DoesNotContain("Welcome to New Haven", LeagueSteward.WelcomeLine);
        }
        finally
        {
            steward.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void PlaceTakesAMovedHallsOldSiteAwayAndClearDoesToo()
    {
        var tinkers = H("tinkers");
        var merchants = H("merchants");
        var oldTinkers = ClusterFGuildHallMoves.All.Single(m => m.Key == "tinkers");
        var oldMerchants = ClusterFGuildHallMoves.All.Single(m => m.Key == "merchants");
        var stockSign = new Sign(SignType.TinkersGuild, SignFacing.North);
        var lookalike = new TinkerGuildmaster();
        var made = new List<IEntity> { stockSign, lookalike };
        try
        {
            Clean();

            // a world placed before the move: the Tinkers' sign at Vesper hiding the stock sign, the guildmaster at
            // his old post; the Merchants' guild house and a door in Magincia, and its guildmaster there
            var oldSignAt = new Point3D(2904, 737, 0);
            stockSign.MoveToWorld(oldSignAt, Map.Trammel);
            stockSign.Visible = false;
            var oldSign = new GuildHallSign("tinkers", 0xBD1, "The Tinkers' Union") { HiddenStockSign = stockSign };
            oldSign.MoveToWorld(oldSignAt, Map.Trammel);
            var oldTinker = new TinkerGuildmaster();
            oldTinker.MoveToWorld(oldTinkers.Npc, Map.Trammel);
            var oldHouse = new GuildHallBuilding("merchants", ClusterFGuildHallPlacement.GuildHouseMulti);
            oldHouse.MoveToWorld(new Point3D(3709, 2176, 20), Map.Trammel);
            var oldDoor = new GuildHallDoor("merchants", DoorFacing.WestCW);
            oldDoor.MoveToWorld(new Point3D(3708, 2182, 27), Map.Trammel);
            var oldMerchant = new MerchantGuildmaster();
            oldMerchant.MoveToWorld(oldMerchants.Npc, Map.Trammel);
            made.AddRange([oldSign, oldTinker, oldHouse, oldDoor, oldMerchant]);
            // a tinker guildmaster elsewhere in Vesper is not the hall's
            lookalike.MoveToWorld(new Point3D(oldTinkers.Npc.X + 6, oldTinkers.Npc.Y, oldTinkers.Npc.Z), Map.Trammel);

            var items = World.Items.Count;
            var dry = ClusterFGuildHallPlacement.Place("tinkers", true, true);
            Assert.Equal(items, World.Items.Count);
            Assert.False(oldSign.Deleted);
            Assert.Contains(dry, l => l.Contains("would take away") && l.Contains("GuildHallSign"));

            ClusterFGuildHallPlacement.Place("tinkers", false, true);
            Assert.True(oldSign.Deleted);
            Assert.True(oldTinker.Deleted);
            Assert.True(stockSign.Visible);
            Assert.False(stockSign.Deleted);
            Assert.False(lookalike.Deleted);
            var sign = ClusterFGuildHallPlacement.Parts(tinkers).OfType<GuildHallSign>().Single();
            Assert.Equal(tinkers.Sign, sign.Location);
            Assert.Equal(tinkers.Npc, ClusterFGuildHallPlacement.FindNpc(tinkers)!.Location);

            // the Merchants left in Magincia: place moves them whole, and a second place adds nothing
            ClusterFGuildHallPlacement.Place("merchants", false, true);
            Assert.True(oldHouse.Deleted);
            Assert.True(oldDoor.Deleted);
            Assert.True(oldMerchant.Deleted);
            Assert.Empty(ClusterFGuildHallPlacement.Parts(merchants).OfType<GuildHallBuilding>());
            Assert.Empty(ClusterFGuildHallPlacement.Parts(merchants).OfType<GuildHallDoor>());
            Assert.Single(ClusterFGuildHallPlacement.Parts(merchants).OfType<GuildHallSign>());
            var count = World.Items.Count + World.Mobiles.Count;
            var again = ClusterFGuildHallPlacement.Place("merchants", false, true);
            Assert.Contains("placed 0 thing(s)", again[^1]);
            Assert.Equal(count, World.Items.Count + World.Mobiles.Count);

            // clear on a world with only the old guildmaster left at Vesper takes him too
            var leftOver = new TinkerGuildmaster();
            made.Add(leftOver);
            leftOver.MoveToWorld(oldTinkers.Npc, Map.Trammel);
            ClusterFGuildHallPlacement.Clear("tinkers", false, true);
            Assert.True(leftOver.Deleted);
            Assert.Null(ClusterFGuildHallPlacement.FindNpc(tinkers));
            Assert.False(lookalike.Deleted);
        }
        finally
        {
            Clean();
            foreach (var e in made)
            {
                e.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void JoiningAMovedGuildNeedsNoHeadquarters()
    {
        ClusterFGuildSystem.EnsureRegistered();
        Clean();
        foreach (var key in Picks.Keys.Where(k => k != "league"))
        {
            var chapters = GuildLocations.For(key);
            Assert.True(chapters.Any(l => l.Town == "New Haven" && l.MapIndex == 1 && InNewHaven(l.Point)), $"{key}: no New Haven chapter");
            Assert.DoesNotContain(chapters, l => l.Point == H(key).Npc);
        }

        Assert.Null(ClusterFGuildHallPlacement.FindNpc(H("healers")));
        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        var account = new Account($"p708{Guid.NewGuid():N}"[..16], "p708-test-only");
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(3660, 2552, 0), Map.Trammel);
        try
        {
            foreach (var key in Picks.Keys.Where(k => k != "league"))
            {
                var def = ClusterFGuildSystem.GetDef(key)!;
                Assert.True(ClusterFGuildSystem.CanJoin(pm, def, out var reason), $"{key}: {reason}");
                ClusterFGuildSystem.Join(pm, def);
                Assert.True(ClusterFGuildSystem.IsJoined(pm, key), key);
            }
        }
        finally
        {
            pm.Delete();
            Accounts.Remove(account);
        }
    }
}
